extends SceneTree
## Run with Godot .NET after building the project:
## --headless --path . --script res://Tests/mod_startup.gd -- invalid|closed|future|diagnosis|crate

var app: Node
var fixture_failed := false

func _initialize() -> void:
    call_deferred("run")

func write_fixture(path: String, contents: String) -> void:
    DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path.get_base_dir()))
    var file := FileAccess.open(path, FileAccess.WRITE)
    if file == null:
        fixture_failed = true
        push_error("Cannot write fixture: " + path)
        return
    file.store_string(contents)
    file.close()

func wait_for_page(file: String) -> Control:
    var deadline := Time.get_ticks_msec() + 6000
    while Time.get_ticks_msec() < deadline:
        for node in app.find_children("*", "Control", true, false):
            if node.is_visible_in_tree() and node.scene_file_path.get_file() == file:
                # The route blocks input until its fade-out finishes.
                await create_timer(0.6).timeout
                return node
        await process_frame
    push_error("Page did not open: " + file)
    quit(1)
    return null

func open_item(page: Node, index := 0) -> bool:
    for node in page.find_children("*", "Button", true, false):
        if node.is_visible_in_tree() and node.get_script() and node.get_script().resource_path.ends_with("/TechMenuItem.cs"):
            if index > 0:
                index -= 1
                continue
            node.pressed.emit()
            return true
    push_error("Menu item missing from " + page.scene_file_path)
    quit(1)
    return false

func run() -> void:
    var args := OS.get_cmdline_user_args()
    if args.size() != 1 or args[0] not in ["invalid", "closed", "future", "diagnosis", "crate"]:
        push_error("Expected one scenario: invalid, closed, future, diagnosis, or crate")
        quit(2)
        return
    var scenario := args[0]
    # Use a fresh user directory for each run, including when a developer runs this locally.
    var folder := "computer-simulator-tests/" + scenario + "-" + str(OS.get_process_id()) + "-" + str(Time.get_ticks_usec())
    ProjectSettings.set_setting("application/config/use_custom_user_dir", true)
    ProjectSettings.set_setting("application/config/custom_user_dir_name", folder)
    if not ProjectSettings.globalize_path("user://").replace("\\", "/").trim_suffix("/").ends_with("/" + folder):
        push_error("Test user directory was not activated")
        quit(2)
        return

    var mod_root := "user://mods/review/"
    write_fixture(mod_root + "mod.json", '{"modId":"review","displayName":"Review","version":"1.0","loadOrder":-200}')
    var future_save := '{"schemaVersion":44,"sections":{"future":{"v":1}}}'
    match scenario:
        "invalid":
            write_fixture(mod_root + "content/commissions/01-01.json", '{"order":-1,"title":"Invalid order","requiredGpuId":"twin","budget":8000,"workloadId":"missing"}')
            write_fixture(mod_root + "content/hardware/motherboards/m24.json", '{"name":"Invalid board","price":1,"slots":null}')
            write_fixture(mod_root + "content/hardware/coolers/d154.json", '{"name":"Missing fan","kind":"air","body_aabb":[{"origin":[0,0,0],"size":[1,1,1]}],"fan_aabb":null}')
            write_fixture(mod_root + "content/benchmarks/standard.json", '{"workloads":null}')
            write_fixture(mod_root + "content/hardware/graphics_cards/invalid.json", '{"name":"Invalid card","price":12,"dimensions":null,"testWatts":[20]}')
        "closed":
            var mode: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://mods/basemod/content/game_modes/commission.json"))
            mode["order"] = int(mode["order"])
            mode["available"] = false
            write_fixture(mod_root + "content/game_modes/commission.json", JSON.stringify(mode))
        "future":
            write_fixture("user://saves/career.json", future_save)
        "diagnosis":
            var dossier := FileAccess.get_file_as_string("res://mods/basemod/content/diagnosis_cases/04-01.json")
            write_fixture(mod_root + "content/diagnosis_cases/04-01.json", dossier.replace("[1, 3]", "[0, 2]").replace('"replacementAllowance": 1', '"replacementAllowance": 2'))
            var supply := FileAccess.get_file_as_string("res://mods/basemod/content/hardware/power_supplies/g750.json")
            write_fixture(mod_root + "content/hardware/power_supplies/g750.json", supply.replace('"length": 160', '"length": 800'))
        "crate":
            var crate := FileAccess.get_file_as_string("res://mods/basemod/content/challenges/first_crate.json")
            write_fixture(mod_root + "content/challenges/first_crate.json", crate.replace('["x3", "twin"]', '["x3"]'))

    if fixture_failed:
        quit(2)
        return
    app = load("res://Src/MainRoot.tscn").instantiate()
    root.add_child(app)
    var menu := await wait_for_page("main_menu.tscn")
    if menu == null or not open_item(menu):
        return
    var modes := await wait_for_page("mode_select.tscn")
    if modes == null or not open_item(modes, {"diagnosis": 3, "crate": 2}.get(scenario, 0)):
        return
    if scenario == "diagnosis":
        var diagnosis := await wait_for_page("diagnosis_case.tscn")
        if diagnosis == null: return
        var choice: OptionButton = diagnosis.get_node("%PsuChoice")
        choice.select(0)
        choice.item_selected.emit(0)
        diagnosis.get_node("%InstallButton").pressed.emit()
        if choice.disabled or not diagnosis.get_node("%RetestButton").disabled or "A1 / B1" not in diagnosis.get_node("%ConfigRam").text:
            push_error("Incompatible replacement, remaining allowance, or injected slots ignored")
            quit(1)
            return
        choice.select(1)
        choice.item_selected.emit(1)
        diagnosis.get_node("%InstallButton").pressed.emit()
        if not choice.disabled or diagnosis.get_node("%RetestButton").disabled:
            push_error("Second replacement did not refresh fit or consume the last allowance")
            quit(1)
            return
        print("PASS ", scenario)
        quit(0)
        return
    if scenario == "crate":
        var bench := await wait_for_page("workbench.tscn")
        if bench == null: return
        var gpu: OptionButton = bench.get_node("%GpuChoice")
        if not gpu.is_item_disabled(1):
            push_error("Out-of-inventory card selectable")
            quit(1)
            return
        gpu.select(1)
        gpu.item_selected.emit(1)
        if not bench.get_node("%PowerRunLoad").disabled:
            push_error("Controller accepted an out-of-inventory card")
            quit(1)
            return
        print("PASS ", scenario)
        quit(0)
        return
    var page := await wait_for_page("mode_brief.tscn" if scenario == "closed" else "commission_brief.tscn")
    if page == null:
        return
    if scenario == "invalid" and not page.find_children("*", "Label", true, false).any(func(label): return "6,900" in label.text):
        push_error("Invalid override displaced the base commission")
        quit(1)
        return
    if scenario == "invalid":
        for button in page.find_children("*", "Button", true, false):
            if button.text == "返回模式选择":
                button.pressed.emit()
                break
        modes = await wait_for_page("mode_select.tscn")
        if modes == null or not open_item(modes, 3): return
        if await wait_for_page("diagnosis_case.tscn") == null: return
    if scenario == "future":
        if FileAccess.get_file_as_string("user://saves/career.json") != future_save:
            push_error("Newer save was moved or modified")
            quit(1)
            return
        for button in page.find_children("*", "Button", true, false):
            if button.is_visible_in_tree() and button.text == "接单并装配" and not button.disabled:
                push_error("Commission accepted an unreadable career save")
                quit(1)
                return
    print("PASS ", scenario)
    quit(0)
