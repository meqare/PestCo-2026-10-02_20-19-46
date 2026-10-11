================================================================================
                          STYLIZED LIBRARY — ASSET PACK
================================================================================

Thank you for purchasing Stylized Library!
This asset pack contains a collection of 48 stylized, hand-crafted 3D props
designed for creating cozy library, study room, and adventure-themed
environments.


--------------------------------------------------------------------------------
  PACKAGE CONTENTS
--------------------------------------------------------------------------------

  48  Unique 3D meshes (.fbx)
  96  Prefabs (48 URP + 48 Built-In)
  48  Materials (24 URP + 24 Built-In, including 1 demo floor per pipeline)
  69  Textures (.png) — Albedo, Normal, MetallicSmoothness
   2  Demo scenes (URP + Built-In)


--------------------------------------------------------------------------------
  INCLUDED MODELS
--------------------------------------------------------------------------------

  Furniture:        Armchair, Bookcase (x2), Shelf, Table, Pouf, Carpet,
                    Fireplace, Pillow

  Books & Scrolls:  Book (x8), Scroll (x7), Stack

  Adventure:        Globe, Gramophone, Telescope, Binocular, Compass,
                    Camera, Clock, Radio, Backpack, Suitcase (x2)

  Fishing:          FishBait, FishBoard

  Pottery:          Pot (x5)

  Miscellaneous:    Bucket, Scoop, Woodlog (x3)


--------------------------------------------------------------------------------
  SUPPORTED RENDER PIPELINES
--------------------------------------------------------------------------------

  - Built-In Render Pipeline
  - Universal Render Pipeline (URP)

  NOTE: HDRP is not supported.


--------------------------------------------------------------------------------
  REQUIREMENTS
--------------------------------------------------------------------------------

  - Unity 2021 LTS or newer


--------------------------------------------------------------------------------
  INSTALLATION
--------------------------------------------------------------------------------

  1. Open your Unity project.
  2. Go to Assets > Import Package > Custom Package.
  3. Select the Stylized_Library.unitypackage file.
  4. Click "Import" to add all assets to your project.
  5. Open the demo scene matching your render pipeline:
     - URP:      Assets/Stylized_Library/Scenes/Demo_URP.unity
     - Built-In: Assets/Stylized_Library/Scenes/Demo_Built-In.unity


--------------------------------------------------------------------------------
  FOLDER STRUCTURE
--------------------------------------------------------------------------------

  Assets/Stylized_Library/
  |
  |-- Materials/
  |   |-- Built-In/          Built-In render pipeline materials (24)
  |   |-- URP/               URP materials (24)
  |
  |-- Meshes/                FBX source meshes (48 models)
  |
  |-- Prefabs/
  |   |-- Built-In/          Ready-to-use Built-In prefabs (48)
  |   |-- URP/               Ready-to-use URP prefabs (48)
  |
  |-- Scenes/                Demo scenes for each pipeline
  |
  |-- Textures/              PBR texture sets organized by object
      |-- T_[ObjectName]/
          |-- *_AlbedoTransparency.png
          |-- *_Normal.png
          |-- *_MetallicSmoothness.png


--------------------------------------------------------------------------------
  TEXTURES
--------------------------------------------------------------------------------

  Format:    PNG (lossless)
  Workflow:  Metallic/Smoothness (PBR)

  Each texture set includes:
    - AlbedoTransparency  (Base color + alpha)
    - Normal              (Tangent-space normal map, OpenGL format)
    - MetallicSmoothness  (Metallic in R, Smoothness in A)


--------------------------------------------------------------------------------
  TEXTURE SETS & SHARED ATLASES
--------------------------------------------------------------------------------

  Some smaller props share a single texture atlas for better performance.

  T_Backpack ........ SM_Backpack
  T_Bookcase_01 ..... SM_Bookcase_01
  T_Bookcase_02 ..... SM_Bookcase_02
  T_Carpet .......... SM_Carpet
  T_Clock ........... SM_Clock
  T_Fireplace ....... SM_Fireplace
  T_FishBait ........ SM_FishBait
  T_FishBoard ....... SM_FishBoard
  T_Globe ........... SM_Globe
  T_Gramophone ...... SM_Gramophone
  T_Shelf ........... SM_Shelf
  T_Table ........... SM_Table
  T_Telescope ....... SM_Telescope
  T_PropsSetA ....... SM_Armchair, SM_Pouf, SM_Pillow
  T_PropsSetB ....... SM_Pot_01 — SM_Pot_05
  T_PropsSetC ....... SM_Binocular, SM_Radio
  T_PropsSetD ....... SM_Camera, SM_Compass
  T_PropsSetE ....... SM_Scroll_01 — SM_Scroll_07
  T_PropsSetF ....... SM_Bucket, SM_Scoop, SM_Stack
  T_PropsSetG ....... SM_Suitcase_01, SM_Suitcase_02
  T_PropsSetH ....... SM_Woodlog_01 — SM_Woodlog_03
  T_PropsSetO ....... SM_Book_01 — SM_Book_04
  T_PropsSetP ....... SM_Book_05 — SM_Book_08


--------------------------------------------------------------------------------
  NAMING CONVENTIONS
--------------------------------------------------------------------------------

  Meshes:      SM_[ObjectName].fbx
  Prefabs:     P_[ObjectName].prefab
  Materials:   M_[ObjectName].mat
  Textures:    T_[ObjectName]_[MapType].png


--------------------------------------------------------------------------------
  TIPS FOR BEST RESULTS
--------------------------------------------------------------------------------

  - Use the prefabs from the folder matching your project's render pipeline.
  - All prefabs are set to position (0, 0, 0) with scale (1, 1, 1).
  - Meshes use real-world scale (1 Unity unit = 1 meter).
  - Check the demo scene for reference on how to arrange the props.
  - Props sharing a texture atlas are designed to be used together
    in the same scene for optimal draw-call batching.


--------------------------------------------------------------------------------
  SUPPORT
--------------------------------------------------------------------------------

  If you have any questions or issues, feel free to reach out
  through the Fab.com messaging system.


--------------------------------------------------------------------------------
  VERSION HISTORY
--------------------------------------------------------------------------------

  v1.0  -  Initial release


================================================================================
