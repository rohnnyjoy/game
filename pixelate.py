import os
import cv2
import json
import numpy as np
from flask import Flask, request, render_template_string, send_from_directory

app = Flask(__name__)
UPLOAD_FOLDER = "uploads"
PROCESSED_FOLDER = "processed"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(PROCESSED_FOLDER, exist_ok=True)

###################################################
# 1) DEFINE YOUR PALETTES IN BGRA FORMAT
#    (B, G, R, A) = (blue, green, red, alpha)
#    The alpha channel is typically 255 for opaque.
#
#    For reference, your original hex colors were:
#       #73121A, #D95204, #8C3503, #D9B1A3, #26201D
#    Converted to BGRA (and each alpha=255):
#       (26,18,115,255), (4,82,217,255), (3,53,140,255),
#       (163,177,217,255), (29,32,38,255)
###################################################

ORANGE_BGRA = [
    (26, 18, 115, 255),  # #73121A => R=115, G=18,  B=26
    (4, 82, 217, 255),  # #D95204 => R=217, G=82,  B=4
    (3, 53, 140, 255),  # #8C3503 => R=140, G=53,  B=3
    (163, 177, 217, 255),  # #D9B1A3 => R=217, G=177, B=163
    (29, 32, 38, 255),  # #26201D => R=38,  G=32,  B=29
]

# You can shift the hue of the original in code, but here we define some
# simpler "manually derived" or "pre-shifted" BGRA palettes. For example:
# We'll do some basic shifts or placeholder values. You can tweak them as needed.

GREEN_BGRA = [
    (26, 115, 18, 255),
    (4, 217, 82, 255),
    (3, 140, 53, 255),
    (163, 217, 177, 255),
    (29, 38, 32, 255),
]

BLUE_BGRA = [
    (115, 18, 26, 255),
    (217, 82, 4, 255),
    (140, 53, 3, 255),
    (217, 177, 163, 255),
    (38, 32, 29, 255),
]

PURPLE_BGRA = [
    (85, 18, 115, 255),
    (65, 82, 217, 255),
    (33, 53, 140, 255),
    (190, 177, 217, 255),
    (45, 32, 38, 255),
]

# A "gray" version can be done by desaturating each color, or by picking
# some standard grayscale values. For example:
GRAY_BGRA = [
    (30, 30, 30, 255),
    (85, 85, 85, 255),
    (128, 128, 128, 255),
    (170, 170, 170, 255),
    (220, 220, 220, 255),
]

DEFAULT_PALETTES = {
    "Orange": ORANGE_BGRA,
    "Green": GREEN_BGRA,
    "Blue": BLUE_BGRA,
    "Purple": PURPLE_BGRA,
    "Gray": GRAY_BGRA,
}

###################################################
# 2) GRAYSCALE UTILITY & MAPPING
###################################################


def bgr_to_gray(b, g, r):
    """
    Compute a single grayscale brightness for a BGR triple.
    We use the common luminance formula:
      gray = 0.114*B + 0.587*G + 0.299*R
    """
    return 0.114 * b + 0.587 * g + 0.299 * r


def map_image_grayscale_closest(image_path, palette_bgra, output_path):
    """
    1) Convert the input image to grayscale.
    2) For each pixel that is not fully transparent, find the palette color
       whose grayscale is closest to that pixel's grayscale, and use it.
    3) Save to output_path.
    """
    # Load image preserving alpha (if present)
    image = cv2.imread(image_path, cv2.IMREAD_UNCHANGED)
    if image is None:
        raise ValueError("Could not open or find the image.")

    # If the image has only 3 channels, add an alpha channel
    if image.ndim == 3 and image.shape[2] == 3:
        image = cv2.cvtColor(image, cv2.COLOR_BGR2BGRA)

    # Separate BGR and alpha
    bgr = image[:, :, :3]
    alpha = image[:, :, 3]

    # Convert entire image to single-channel grayscale
    gray_img = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY).astype(np.float32)

    # Precompute palette grayscale
    # Filter out any palette color that is fully transparent if you want to skip them
    valid_colors = []
    valid_gray = []
    for b, g, r, a in palette_bgra:
        if a == 0:
            continue  # skip transparent
        valid_colors.append((b, g, r, a))
        val_g = bgr_to_gray(b, g, r)
        valid_gray.append(val_g)
    valid_gray = np.array(valid_gray, dtype=np.float32)

    # Flatten for easy iteration
    h, w = gray_img.shape
    flat_gray = gray_img.reshape(-1)
    flat_alpha = alpha.reshape(-1)
    mapped_bgr = np.zeros((h * w, 3), dtype=np.uint8)

    # For each pixel, pick the palette color whose grayscale is closest
    for i, gval in enumerate(flat_gray):
        if flat_alpha[i] == 0:
            # Fully transparent => keep it transparent
            continue
        diffs = np.abs(valid_gray - gval)
        best_idx = np.argmin(diffs)
        b, g, r, _ = valid_colors[best_idx]
        mapped_bgr[i] = (b, g, r)

    # Reshape back
    mapped_bgr = mapped_bgr.reshape(h, w, 3)
    final = np.dstack([mapped_bgr, alpha])

    # Save
    cv2.imwrite(output_path, final)


###################################################
# 3) FLASK ROUTES
###################################################


@app.route("/", methods=["GET", "POST"])
def upload_file():
    if request.method == "POST":
        file = request.files["file"]
        palette_choice = request.form.get("palette_choice", "Orange")
        chosen_palette = DEFAULT_PALETTES.get(palette_choice, ORANGE_BGRA)

        if file:
            input_path = os.path.join(UPLOAD_FOLDER, file.filename)
            filename_no_ext, ext = os.path.splitext(file.filename)
            output_filename = f"mapped_{filename_no_ext}.png"
            output_path = os.path.join(PROCESSED_FOLDER, output_filename)

            file.save(input_path)

            # Perform grayscale + closest color mapping
            map_image_grayscale_closest(input_path, chosen_palette, output_path)

            # Show result
            return render_template_string(
                """
                <!doctype html>
                <title>Mapped Image</title>
                <h1>Color Palette Mapped Image</h1>
                <img src="/processed/{{ filename }}" style="max-width: 100%; height: auto;"/>
                <br>
                <a href="/processed/{{ filename }}" download>Download Mapped Image</a>
                <br><br>
                <a href="/">Upload Another Image</a>
                """,
                filename=output_filename,
            )

    # Build the palette dropdown
    palette_options = ""
    for key in DEFAULT_PALETTES.keys():
        palette_options += f'<option value="{key}">{key}</option>'

    return f"""
    <!doctype html>
    <title>Upload Image for Grayscale Palette Mapping</title>
    <h1>Upload Image for Grayscale + Closest-Color Mapping</h1>
    <form method="post" enctype="multipart/form-data">
        <div>
            <label for="file">Select image file (PNG with transparency is supported):</label>
            <input type="file" name="file" id="file" required>
        </div>
        <br>
        <label for="palette_choice">Choose a palette:</label>
        <select id="palette_choice" name="palette_choice">
            {palette_options}
        </select>
        <br><br>
        <input type="submit" value="Upload and Map">
    </form>
    """


@app.route("/processed/<filename>")
def processed_file(filename):
    return send_from_directory(os.path.abspath(PROCESSED_FOLDER), filename)


if __name__ == "__main__":
    app.run(debug=True, host="127.0.0.1", port=5000)
