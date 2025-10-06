@tool
extends EditorScript

# Generates 2x and 3x copies of all PNGs in res://assets/ui/1x using nearest-neighbor.
# Usage (Editor): Project > Tools > Run (with this script open), or
# Usage (CLI): godot --headless --script res://scripts/generate_ui_scales.gd

func _run() -> void:
    var base_dir := "res://assets/ui/1x"
    var dir2 := "res://assets/ui/2x"
    var dir3 := "res://assets/ui/3x"

    var da := DirAccess.open("res://")
    if da == null:
        push_error("Failed to open res://")
        return

    da.make_dir_recursive("assets/ui/2x")
    da.make_dir_recursive("assets/ui/3x")

    var files := DirAccess.get_files_at(base_dir)
    if files.is_empty():
        push_warning("No files found in %s" % base_dir)
    for f in files:
        if not f.to_lower().ends_with(".png"):
            continue
        var src_path := base_dir + "/" + f
        var img := Image.new()
        var err := img.load(src_path)
        if err != OK:
            push_error("Failed to load %s (err %d)" % [src_path, err])
            continue

        # 2x
        var img2 := img.duplicate()
        img2.resize(img2.get_width() * 2, img2.get_height() * 2, Image.INTERPOLATE_NEAREST)
        var out2 := dir2 + "/" + f
        var err2 := img2.save_png(out2)
        if err2 != OK:
            push_error("Failed to save %s (err %d)" % [out2, err2])

        # 3x
        var img3 := img.duplicate()
        img3.resize(img3.get_width() * 3, img3.get_height() * 3, Image.INTERPOLATE_NEAREST)
        var out3 := dir3 + "/" + f
        var err3 := img3.save_png(out3)
        if err3 != OK:
            push_error("Failed to save %s (err %d)" % [out3, err3])

    print("UI scales generated in %s and %s" % [dir2, dir3])

