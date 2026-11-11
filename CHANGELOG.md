# Changelog

## Unreleased

- Fixed: deconstructing, uninstalling or reinstalling a full dresser or wardrobe could silently delete stored apparel
  when no free cell was within about 13 tiles (TT-001). The furniture is now emptied first, falling back to the
  nearest free spot anywhere on the map.

## 1.0.0 (2026-11-11)

First release, for personal use (not on the Steam Workshop).

- Every dresser is apparel-only storage for 20 items, Critical priority by default.
- A wardrobe for 30 items: Vanilla Furniture Expanded's when it is active, otherwise our own with its own art.
- Dressers and wardrobes link into one storage group; shelves can't join it.
- Dressers built before the mod was added are converted on load, keeping quality and art.
- Adding or removing Vanilla Furniture Expanded keeps built wardrobes, their contents and their priority.
