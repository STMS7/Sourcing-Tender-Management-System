# Zaytouna footer map

MapLibre GL JS 5.24.0 is vendored in wwwroot/lib/maplibre with its license. No Google API script/key is needed.

The local irbid.json is adapted from OpenFreeMap Liberty (https://tiles.openfreemap.org/styles/liberty), with olive/cream colors. Vector data remains hosted by OpenFreeMap; original source attribution is preserved. Real elevation is loaded from https://tiles.mapterhorn.com/tilejson.json with its provider attribution. Building extrusion uses only supplied render_height/render_min_height, defaulting to zero when unknown, not invented building heights.

The point [35.85, 32.5556] identifies the city of Irbid, not a company premises. The camera approaches once per page view. Replay, Skip, Explore/Done, Escape and reduced motion are supported. Wheel zoom stays disabled to preserve page scrolling.

A real OpenStreetMap embed is the initial/no-JavaScript/WebGL/network fallback, with a Google Maps city link always available. Remote map/terrain services still require an internet connection and are subject to their availability. The appearance is a styled geographic terrain map, not Google photorealistic imagery.

The unfinished GoogleMapsOptions configuration is retained for compatibility with earlier teammate work but is not used by this footer map.

Map labels use English/Latin fields only; non-Latin secondary labels are omitted to avoid unshaped Arabic text. The Arabic HTML city heading remains correctly rendered.
