#! /usr/bin/env python

# This is a pretty hacky script to generate apriltags.
# you'll need to change the hard-coded parameters below
# it also assumes the apriltag-imgs repository has been cloned here.
from glob import glob
from os import path, makedirs
from os.path import splitext, basename
from subprocess import run

N_TAGS = 8 # how many tags to generate
DOTS_PER_CM = 100 # resolution of the resulting image
# remember that apriltags are measured along the square edge, so for older (v2)
# tags, that's along the outside. In newer v3 tags the edge is inside the shape.
TAG_WIDTH_PIXELS = 5 # number of pixels along the square edge of the tag
TAG_FAMILIES = [('tagStandard41h12', 5), ('tag25h9', 7), ('tag16h5', 6)]
TAG_SIZES = [4, 5, 6, 7, 11] # what sizes to generate

root_dir = path.dirname(__file__)
dest_dir = path.join(root_dir, 'apriltag-imgs', 'pdf')
makedirs(dest_dir, exist_ok=True)
TAG_WIDTH_PIXELS = 5
for (family, tag_width) in TAG_FAMILIES:
    img_dir = path.join(root_dir, 'apriltag-imgs', family)
    src_imgs = sorted(glob(path.join(img_dir, "tag*")))[:N_TAGS]
    # some small tag sizes for testing, and full-page sized
    for size_cm in TAG_SIZES:
        sized_dest_dir = path.join(dest_dir, family, f'{size_cm}cm')
        makedirs(sized_dest_dir, exist_ok=True)
        generated_pdfs = []
        for src_img in src_imgs:
            file_base = basename(splitext(src_img)[0])
            pdf_path = path.join(sized_dest_dir, file_base ) + '.pdf'
            scale_pct = size_cm * DOTS_PER_CM / tag_width * 100
            run(['convert',
                '-units', 'PixelsPerCentimeter',
                src_img,
                '-sample', f'{scale_pct}%',
                '-density', str(DOTS_PER_CM),
                '-background', 'white',
                '-gravity', 'South', '-splice', '0x60',
                '-pointsize', '40', '-annotate', '0x0', f"{family} {file_base[-5:]} {size_cm}cm",
                pdf_path])
            print(f'generated {pdf_path}')
            generated_pdfs.append(pdf_path)

        run(['gs', '-dBATCH', '-dNOPAUSE', '-q', '-sDEVICE=pdfwrite',
            f'-sOutputFile={path.join(sized_dest_dir, "alltags.pdf")}',
            *generated_pdfs])
