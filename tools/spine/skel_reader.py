#!/usr/bin/env python3
"""Spine 4.2 binary skeleton (.skel) reader.

Faithful Python port of the official loader
``spine-libgdx/.../SkeletonBinary.java`` (spine-runtimes branch 4.2), including
the embedded ``SkeletonInput`` (libgdx ``DataInput``) primitives.

``read_skel(path)`` returns a JSON-like dict modelled on Spine's JSON export:

    {skeleton, bones, slots, ik, transform, path, physics, skins, events, animations}

Notes on representation (differences from the official JSON export):
  * Values are not scaled (scale = 1, exactly like loading with the default scale).
  * Every key carries an explicit ``time`` (JSON export omits time 0).
  * Curves: a key's ``curve`` describes the segment from that key to the next one.
    Absent => linear, ``"stepped"`` => stepped, list => bezier control points
    [cx1, cy1, cx2, cy2] per animated value (concatenated, as in the JSON format).
  * Colors are "rrggbbaa" / "rrggbb" hex strings.
  * Draw order keys carry both ``offsets`` (as in JSON) and the resolved full
    ``order`` (slot names, back to front).
  * Skins are emitted as a list (JSON 4.x style) *and* ``skins_by_name``
    ({skin: {slot: {attachment: {...}}}}).

Usage: python3 skel_reader.py input.skel output.json
"""

import json
import struct
import sys

INHERIT = ["normal", "onlyTranslation", "noRotationOrReflection", "noScale", "noScaleOrReflection"]
BLEND = ["normal", "additive", "multiply", "screen"]
ATTACHMENT_TYPE = ["region", "boundingbox", "mesh", "linkedmesh", "path", "point", "clipping", "sequence"]
POSITION_MODE = ["fixed", "percent"]
SPACING_MODE = ["length", "fixed", "percent", "proportional"]
ROTATE_MODE = ["tangent", "chain", "chainScale"]
SEQUENCE_MODE = ["hold", "once", "loop", "pingpong", "onceReverse", "loopReverse", "pingpongReverse"]

BONE_ROTATE, BONE_TRANSLATE, BONE_TRANSLATEX, BONE_TRANSLATEY = 0, 1, 2, 3
BONE_SCALE, BONE_SCALEX, BONE_SCALEY = 4, 5, 6
BONE_SHEAR, BONE_SHEARX, BONE_SHEARY, BONE_INHERIT = 7, 8, 9, 10
BONE_TIMELINE_NAMES = {
    BONE_ROTATE: ("rotate", ("value",)),
    BONE_TRANSLATE: ("translate", ("x", "y")),
    BONE_TRANSLATEX: ("translatex", ("value",)),
    BONE_TRANSLATEY: ("translatey", ("value",)),
    BONE_SCALE: ("scale", ("x", "y")),
    BONE_SCALEX: ("scalex", ("value",)),
    BONE_SCALEY: ("scaley", ("value",)),
    BONE_SHEAR: ("shear", ("x", "y")),
    BONE_SHEARX: ("shearx", ("value",)),
    BONE_SHEARY: ("sheary", ("value",)),
}

SLOT_ATTACHMENT, SLOT_RGBA, SLOT_RGB, SLOT_RGBA2, SLOT_RGB2, SLOT_ALPHA = 0, 1, 2, 3, 4, 5
ATTACHMENT_DEFORM, ATTACHMENT_SEQUENCE = 0, 1
PATH_POSITION, PATH_SPACING, PATH_MIX = 0, 1, 2
PHYSICS_INERTIA, PHYSICS_STRENGTH, PHYSICS_DAMPING = 0, 1, 2
PHYSICS_MASS, PHYSICS_WIND, PHYSICS_GRAVITY, PHYSICS_MIX, PHYSICS_RESET = 4, 5, 6, 7, 8
PHYSICS_TIMELINE_NAMES = {
    PHYSICS_INERTIA: "inertia", PHYSICS_STRENGTH: "strength", PHYSICS_DAMPING: "damping",
    PHYSICS_MASS: "mass", PHYSICS_WIND: "wind", PHYSICS_GRAVITY: "gravity", PHYSICS_MIX: "mix",
}
CURVE_LINEAR, CURVE_STEPPED, CURVE_BEZIER = 0, 1, 2


class SkelError(Exception):
    pass


def _f(v):
    """Shorten a float32 value to a readable round-tripping repr."""
    return float("%.7g" % v)


def _hex_rgba8888(v):
    return "%08x" % (v & 0xFFFFFFFF)


def _hex_rgb888(v):
    return "%06x" % (v & 0xFFFFFF)


class SkeletonInput:
    """Port of libgdx DataInput (big-endian DataInputStream) + SkeletonInput."""

    def __init__(self, data):
        self.data = data
        self.pos = 0
        self.strings = []

    def _take(self, n):
        if self.pos + n > len(self.data):
            raise SkelError("Unexpected EOF at %d (need %d bytes)" % (self.pos, n))
        b = self.data[self.pos:self.pos + n]
        self.pos += n
        return b

    # InputStream.read(): unsigned byte, -1 at EOF (EOF treated as error here).
    def read(self):
        return self._take(1)[0]

    def readUnsignedByte(self):
        return self._take(1)[0]

    def readByte(self):
        return struct.unpack(">b", self._take(1))[0]

    def readBoolean(self):
        return self._take(1)[0] != 0

    def readLong(self):
        return struct.unpack(">q", self._take(8))[0]

    def readFloat(self):
        return struct.unpack(">f", self._take(4))[0]

    def readInt(self, optimize_positive=None):
        if optimize_positive is None:  # DataInputStream.readInt(): 4 bytes big-endian
            return struct.unpack(">i", self._take(4))[0]
        # libgdx DataInput.readInt(boolean): varint, 7 bits per byte, little-endian groups.
        b = self.read()
        result = b & 0x7F
        if b & 0x80:
            b = self.read()
            result |= (b & 0x7F) << 7
            if b & 0x80:
                b = self.read()
                result |= (b & 0x7F) << 14
                if b & 0x80:
                    b = self.read()
                    result |= (b & 0x7F) << 21
                    if b & 0x80:
                        b = self.read()
                        result |= (b & 0x7F) << 28
        result &= 0xFFFFFFFF
        if not optimize_positive:
            result = (result >> 1) ^ (-(result & 1) & 0xFFFFFFFF)
        if result & 0x80000000:
            result -= 1 << 32
        return result

    def readStringRef(self):
        index = self.readInt(True)
        return None if index == 0 else self.strings[index - 1]

    def readString(self):
        byte_count = self.readInt(True)
        if byte_count == 0:
            return None
        if byte_count == 1:
            return ""
        byte_count -= 1
        chars = []
        i = 0
        while i < byte_count:
            b = self.read()
            hi = b >> 4
            if hi in (12, 13):
                chars.append(chr(((b & 0x1F) << 6 | self.read() & 0x3F) & 0xFFFF))
                i += 2
            elif hi == 14:
                c = (b & 0x0F) << 12 | (self.read() & 0x3F) << 6
                c |= self.read() & 0x3F
                chars.append(chr(c & 0xFFFF))
                i += 3
            else:
                chars.append(chr(b))
                i += 1
        return "".join(chars)


class SkeletonBinaryReader:
    def __init__(self, data):
        self.input = SkeletonInput(data)
        self.linked_meshes = []
        # Internal attachment objects, per skin index: {(slotIndex, name): dict}
        self._skin_attachments = []
        self.slot_names = []
        self.bone_names = []

    # ------------------------------------------------------------------ main
    def read(self):
        inp = self.input
        out = {}
        skeleton = {}
        h = inp.readLong()
        skeleton["hash"] = None if h == 0 else str(h)
        version = inp.readString()
        skeleton["spine"] = version if version else None
        skeleton["x"] = _f(inp.readFloat())
        skeleton["y"] = _f(inp.readFloat())
        skeleton["width"] = _f(inp.readFloat())
        skeleton["height"] = _f(inp.readFloat())
        skeleton["referenceScale"] = _f(inp.readFloat())
        nonessential = inp.readBoolean()
        skeleton["nonessential"] = nonessential
        if nonessential:
            skeleton["fps"] = _f(inp.readFloat())
            p = inp.readString()
            skeleton["images"] = p if p else None
            p = inp.readString()
            skeleton["audio"] = p if p else None
        out["skeleton"] = skeleton
        self.nonessential = nonessential

        # Strings.
        n = inp.readInt(True)
        inp.strings = [inp.readString() for _ in range(n)]

        # Bones.
        bones = []
        n = inp.readInt(True)
        for i in range(n):
            name = inp.readString()
            parent = None if i == 0 else bones[inp.readInt(True)]["name"]
            b = {"name": name, "index": i}
            if parent is not None:
                b["parent"] = parent
            b["rotation"] = _f(inp.readFloat())
            b["x"] = _f(inp.readFloat())
            b["y"] = _f(inp.readFloat())
            b["scaleX"] = _f(inp.readFloat())
            b["scaleY"] = _f(inp.readFloat())
            b["shearX"] = _f(inp.readFloat())
            b["shearY"] = _f(inp.readFloat())
            b["length"] = _f(inp.readFloat())
            b["inherit"] = INHERIT[inp.readByte()]
            b["skin"] = inp.readBoolean()
            if nonessential:
                b["color"] = _hex_rgba8888(inp.readInt())
                b["icon"] = inp.readString()
                b["visible"] = inp.readBoolean()
            bones.append(b)
        out["bones"] = bones
        self.bone_names = [b["name"] for b in bones]
        bn = self.bone_names

        # Slots.
        slots = []
        n = inp.readInt(True)
        for i in range(n):
            s = {"name": inp.readString(), "index": i}
            s["bone"] = bn[inp.readInt(True)]
            s["color"] = _hex_rgba8888(inp.readInt())
            dark = inp.readInt()
            if dark != -1:
                s["dark"] = _hex_rgb888(dark)
            s["attachment"] = inp.readStringRef()
            s["blend"] = BLEND[inp.readInt(True)]
            if nonessential:
                s["visible"] = inp.readBoolean()
            slots.append(s)
        out["slots"] = slots
        self.slot_names = [s["name"] for s in slots]

        # IK constraints.
        ik = []
        n = inp.readInt(True)
        for i in range(n):
            d = {"name": inp.readString(), "order": inp.readInt(True)}
            nn = inp.readInt(True)
            d["bones"] = [bn[inp.readInt(True)] for _ in range(nn)]
            d["target"] = bn[inp.readInt(True)]
            flags = inp.read()
            d["skin"] = (flags & 1) != 0
            d["bendPositive"] = (flags & 2) != 0
            d["compress"] = (flags & 4) != 0
            d["stretch"] = (flags & 8) != 0
            d["uniform"] = (flags & 16) != 0
            # IkConstraintData.mix defaults to 0; flag 32 => mix = (flag 64 ? float : 1).
            d["mix"] = 0.0
            if flags & 32:
                d["mix"] = _f(inp.readFloat()) if (flags & 64) else 1.0
            d["softness"] = _f(inp.readFloat()) if (flags & 128) else 0.0
            ik.append(d)
        out["ik"] = ik

        # Transform constraints.
        tc = []
        n = inp.readInt(True)
        for i in range(n):
            d = {"name": inp.readString(), "order": inp.readInt(True)}
            nn = inp.readInt(True)
            d["bones"] = [bn[inp.readInt(True)] for _ in range(nn)]
            d["target"] = bn[inp.readInt(True)]
            flags = inp.read()
            d["skin"] = (flags & 1) != 0
            d["local"] = (flags & 2) != 0
            d["relative"] = (flags & 4) != 0
            d["rotation"] = _f(inp.readFloat()) if flags & 8 else 0.0
            d["x"] = _f(inp.readFloat()) if flags & 16 else 0.0
            d["y"] = _f(inp.readFloat()) if flags & 32 else 0.0
            d["scaleX"] = _f(inp.readFloat()) if flags & 64 else 0.0
            d["scaleY"] = _f(inp.readFloat()) if flags & 128 else 0.0
            flags = inp.read()
            d["shearY"] = _f(inp.readFloat()) if flags & 1 else 0.0
            # TransformConstraintData mix defaults are 0 (only set when flagged).
            d["mixRotate"] = _f(inp.readFloat()) if flags & 2 else 0.0
            d["mixX"] = _f(inp.readFloat()) if flags & 4 else 0.0
            d["mixY"] = _f(inp.readFloat()) if flags & 8 else 0.0
            d["mixScaleX"] = _f(inp.readFloat()) if flags & 16 else 0.0
            d["mixScaleY"] = _f(inp.readFloat()) if flags & 32 else 0.0
            d["mixShearY"] = _f(inp.readFloat()) if flags & 64 else 0.0
            tc.append(d)
        out["transform"] = tc

        # Path constraints.
        pc = []
        self.path_data = pc
        n = inp.readInt(True)
        for i in range(n):
            d = {"name": inp.readString(), "order": inp.readInt(True)}
            d["skin"] = inp.readBoolean()
            nn = inp.readInt(True)
            d["bones"] = [bn[inp.readInt(True)] for _ in range(nn)]
            d["target"] = self.slot_names[inp.readInt(True)]
            flags = inp.read()
            d["positionMode"] = POSITION_MODE[flags & 1]
            d["spacingMode"] = SPACING_MODE[(flags >> 1) & 3]
            d["rotateMode"] = ROTATE_MODE[(flags >> 3) & 3]
            d["rotation"] = _f(inp.readFloat()) if flags & 128 else 0.0
            d["position"] = _f(inp.readFloat())
            d["spacing"] = _f(inp.readFloat())
            d["mixRotate"] = _f(inp.readFloat())
            d["mixX"] = _f(inp.readFloat())
            d["mixY"] = _f(inp.readFloat())
            pc.append(d)
        out["path"] = pc

        # Physics constraints.
        ph = []
        n = inp.readInt(True)
        for i in range(n):
            d = {"name": inp.readString(), "order": inp.readInt(True)}
            d["bone"] = bn[inp.readInt(True)]
            flags = inp.read()
            d["skin"] = (flags & 1) != 0
            d["x"] = _f(inp.readFloat()) if flags & 2 else 0.0
            d["y"] = _f(inp.readFloat()) if flags & 4 else 0.0
            d["rotate"] = _f(inp.readFloat()) if flags & 8 else 0.0
            d["scaleX"] = _f(inp.readFloat()) if flags & 16 else 0.0
            d["shearX"] = _f(inp.readFloat()) if flags & 32 else 0.0
            d["limit"] = _f(inp.readFloat()) if flags & 64 else 5000.0
            d["fps"] = inp.readUnsignedByte()
            d["step"] = 1.0 / d["fps"] if d["fps"] else None
            d["inertia"] = _f(inp.readFloat())
            d["strength"] = _f(inp.readFloat())
            d["damping"] = _f(inp.readFloat())
            mass_inverse = _f(inp.readFloat()) if flags & 128 else 1.0
            d["massInverse"] = mass_inverse
            d["mass"] = _f(1.0 / mass_inverse) if mass_inverse else None
            d["wind"] = _f(inp.readFloat())
            d["gravity"] = _f(inp.readFloat())
            flags = inp.read()
            d["inertiaGlobal"] = bool(flags & 1)
            d["strengthGlobal"] = bool(flags & 2)
            d["dampingGlobal"] = bool(flags & 4)
            d["massGlobal"] = bool(flags & 8)
            d["windGlobal"] = bool(flags & 16)
            d["gravityGlobal"] = bool(flags & 32)
            d["mixGlobal"] = bool(flags & 64)
            d["mix"] = _f(inp.readFloat()) if flags & 128 else 1.0
            ph.append(d)
        out["physics"] = ph
        self.ik_names = [d["name"] for d in ik]
        self.tc_names = [d["name"] for d in tc]
        self.pc_names = [d["name"] for d in pc]
        self.ph_names = [d["name"] for d in ph]

        # Default skin.
        skins = []
        default_skin = self.read_skin(len(skins), True)
        if default_skin is not None:
            skins.append(default_skin)
        # Skins.
        n = inp.readInt(True)
        for _ in range(n):
            skins.append(self.read_skin(len(skins), False))

        # Linked meshes.
        for lm in self.linked_meshes:
            if lm["skinIndex"] >= len(self._skin_attachments):
                raise SkelError("Linked mesh skin index out of range: %d" % lm["skinIndex"])
            parent = self._skin_attachments[lm["skinIndex"]].get((lm["slotIndex"], lm["parent"]))
            if parent is None:
                raise SkelError("Parent mesh not found: " + str(lm["parent"]))
            mesh = lm["mesh"]
            # setParentMesh copies bones/vertices/regionUVs/triangles/hullLength/worldVerticesLength.
            for k in ("_bones", "_vertices", "_worldVerticesLength"):
                mesh[k] = parent.get(k)
            for k in ("vertexCount", "weighted", "hull", "influenceBones", "maxBonesPerVertex"):
                if k in parent:
                    mesh[k] = parent[k]
            mesh["_timelineAttachment"] = lm["parent"] if lm["inheritTimelines"] else mesh["name"]
        self.linked_meshes = []

        out["skins"] = skins
        out["skins_by_name"] = {s["name"]: s["attachments"] for s in skins}
        self.skins = skins

        # Events.
        events = {}
        self.event_list = []
        n = inp.readInt(True)
        for _ in range(n):
            name = inp.readString()
            e = {"int": inp.readInt(False), "float": _f(inp.readFloat()), "string": inp.readString()}
            e["audio"] = inp.readString()
            if e["audio"] is not None:
                e["volume"] = _f(inp.readFloat())
                e["balance"] = _f(inp.readFloat())
            events[name] = e
            self.event_list.append((name, e))
        out["events"] = events

        # Animations.
        anims = {}
        n = inp.readInt(True)
        for _ in range(n):
            name = inp.readString()
            anims[name] = self.read_animation()
        out["animations"] = anims

        # Strip internal fields.
        for s in skins:
            for slot_atts in s["attachments"].values():
                for a in slot_atts.values():
                    for k in [k for k in a if k.startswith("_")]:
                        del a[k]
        out["_bytes_read"] = inp.pos
        out["_bytes_total"] = len(inp.data)
        return out

    # ------------------------------------------------------------------ skins
    def read_skin(self, skin_index, default_skin):
        inp = self.input
        skin = {}
        if default_skin:
            slot_count = inp.readInt(True)
            if slot_count == 0:
                return None
            skin["name"] = "default"
        else:
            skin["name"] = inp.readString()
            if self.nonessential:
                skin["color"] = _hex_rgba8888(inp.readInt())
            nb = inp.readInt(True)
            skin["bones"] = [self.bone_names[inp.readInt(True)] for _ in range(nb)]
            skin["ik"] = [self.ik_names[inp.readInt(True)] for _ in range(inp.readInt(True))]
            skin["transform"] = [self.tc_names[inp.readInt(True)] for _ in range(inp.readInt(True))]
            skin["path"] = [self.pc_names[inp.readInt(True)] for _ in range(inp.readInt(True))]
            skin["physics"] = [self.ph_names[inp.readInt(True)] for _ in range(inp.readInt(True))]
            slot_count = inp.readInt(True)
        atts = {}
        internal = {}
        self._skin_attachments.append(internal)
        for _ in range(slot_count):
            slot_index = inp.readInt(True)
            nn = inp.readInt(True)
            for _ in range(nn):
                name = inp.readStringRef()
                a = self.read_attachment(skin_index, slot_index, name)
                if a is not None:
                    atts.setdefault(self.slot_names[slot_index], {})[name] = a
                    internal[(slot_index, name)] = a
        skin["attachments"] = atts
        return skin

    def read_sequence(self):
        inp = self.input
        return {"count": inp.readInt(True), "start": inp.readInt(True),
                "digits": inp.readInt(True), "setup": inp.readInt(True)}

    def read_vertices(self, weighted):
        inp = self.input
        vertex_count = inp.readInt(True)
        length = vertex_count << 1
        if not weighted:
            return {"length": length, "bones": None, "vertices": [inp.readFloat() for _ in range(length)],
                    "vertexCount": vertex_count}
        weights = []
        bones = []
        for _ in range(vertex_count):
            bone_count = inp.readInt(True)
            bones.append(bone_count)
            for _ in range(bone_count):
                bones.append(inp.readInt(True))
                weights.append(inp.readFloat())
                weights.append(inp.readFloat())
                weights.append(inp.readFloat())
        return {"length": length, "bones": bones, "vertices": weights, "vertexCount": vertex_count}

    def _vertices_json(self, v):
        """Spine JSON 'vertices' layout: unweighted = x,y...; weighted = boneCount, (bone, x, y, w)*..."""
        if v["bones"] is None:
            return [_f(x) for x in v["vertices"]]
        out = []
        bi = 0
        wi = 0
        bones = v["bones"]
        w = v["vertices"]
        while bi < len(bones):
            c = bones[bi]
            bi += 1
            out.append(c)
            for _ in range(c):
                out.append(bones[bi])
                bi += 1
                out.extend([_f(w[wi]), _f(w[wi + 1]), _f(w[wi + 2])])
                wi += 3
        return out

    @staticmethod
    def _bone_influence_stats(v):
        if v["bones"] is None:
            return None
        bones = v["bones"]
        used = set()
        max_per_vertex = 0
        bi = 0
        while bi < len(bones):
            c = bones[bi]
            max_per_vertex = max(max_per_vertex, c)
            used.update(bones[bi + 1:bi + 1 + c])
            bi += 1 + c
        return sorted(used), max_per_vertex

    def _attach_vertex_info(self, a, v):
        a["vertexCount"] = v["vertexCount"]
        a["weighted"] = v["bones"] is not None
        a["vertices"] = self._vertices_json(v)
        a["_bones"] = v["bones"]
        a["_vertices"] = v["vertices"]
        a["_worldVerticesLength"] = v["length"]
        st = self._bone_influence_stats(v)
        if st:
            a["influenceBones"] = [self.bone_names[i] for i in st[0]]
            a["maxBonesPerVertex"] = st[1]

    def read_attachment(self, skin_index, slot_index, attachment_name):
        inp = self.input
        nonessential = self.nonessential
        flags = inp.readByte()
        name = inp.readStringRef() if (flags & 8) else attachment_name
        atype = ATTACHMENT_TYPE[flags & 0b111]
        a = {"type": atype, "name": name}
        if atype == "region":
            path = inp.readStringRef() if flags & 16 else None
            color = inp.readInt() if flags & 32 else -1
            sequence = self.read_sequence() if flags & 64 else None
            rotation = inp.readFloat() if flags & 128 else 0.0
            a["path"] = path if path is not None else name
            a["x"] = _f(inp.readFloat())
            a["y"] = _f(inp.readFloat())
            a["scaleX"] = _f(inp.readFloat())
            a["scaleY"] = _f(inp.readFloat())
            a["rotation"] = _f(rotation)
            a["width"] = _f(inp.readFloat())
            a["height"] = _f(inp.readFloat())
            a["color"] = _hex_rgba8888(color)
            if sequence:
                a["sequence"] = sequence
            return a
        if atype == "boundingbox":
            v = self.read_vertices((flags & 16) != 0)
            color = inp.readInt() if nonessential else 0
            self._attach_vertex_info(a, v)
            if nonessential:
                a["color"] = _hex_rgba8888(color)
            return a
        if atype == "mesh":
            path = inp.readStringRef() if flags & 16 else name
            color = inp.readInt() if flags & 32 else -1
            sequence = self.read_sequence() if flags & 64 else None
            hull_length = inp.readInt(True)
            v = self.read_vertices((flags & 128) != 0)
            uvs = [inp.readFloat() for _ in range(v["length"])]
            tri_count = (v["length"] - hull_length - 2) * 3
            triangles = [inp.readInt(True) & 0xFFFF for _ in range(tri_count)]
            triangles = [t - 0x10000 if t & 0x8000 else t for t in triangles]  # (short) cast
            edges = None
            width = height = 0.0
            if nonessential:
                edges = [inp.readInt(True) for _ in range(inp.readInt(True))]
                edges = [(t & 0xFFFF) - 0x10000 if t & 0x8000 else t & 0xFFFF for t in edges]
                width = inp.readFloat()
                height = inp.readFloat()
            a["path"] = path
            a["color"] = _hex_rgba8888(color)
            if sequence:
                a["sequence"] = sequence
            a["hull"] = hull_length
            self._attach_vertex_info(a, v)
            a["uvs"] = [_f(x) for x in uvs]
            a["triangles"] = triangles
            if nonessential:
                a["edges"] = edges
                a["width"] = _f(width)
                a["height"] = _f(height)
            return a
        if atype == "linkedmesh":
            path = inp.readStringRef() if flags & 16 else name
            color = inp.readInt() if flags & 32 else -1
            sequence = self.read_sequence() if flags & 64 else None
            inherit_timelines = (flags & 128) != 0
            skin_idx = inp.readInt(True)
            parent = inp.readStringRef()
            width = height = 0.0
            if nonessential:
                width = inp.readFloat()
                height = inp.readFloat()
            a["path"] = path
            a["color"] = _hex_rgba8888(color)
            if sequence:
                a["sequence"] = sequence
            a["skinIndex"] = skin_idx
            a["parent"] = parent
            a["timelines"] = inherit_timelines
            if nonessential:
                a["width"] = _f(width)
                a["height"] = _f(height)
            self.linked_meshes.append({"mesh": a, "skinIndex": skin_idx, "slotIndex": slot_index,
                                       "parent": parent, "inheritTimelines": inherit_timelines})
            return a
        if atype == "path":
            a["closed"] = (flags & 16) != 0
            a["constantSpeed"] = (flags & 32) != 0
            v = self.read_vertices((flags & 64) != 0)
            a["lengths"] = [_f(inp.readFloat()) for _ in range(v["length"] // 6)]
            color = inp.readInt() if nonessential else 0
            self._attach_vertex_info(a, v)
            if nonessential:
                a["color"] = _hex_rgba8888(color)
            return a
        if atype == "point":
            a["rotation"] = _f(inp.readFloat())
            a["x"] = _f(inp.readFloat())
            a["y"] = _f(inp.readFloat())
            color = inp.readInt() if nonessential else 0
            if nonessential:
                a["color"] = _hex_rgba8888(color)
            return a
        if atype == "clipping":
            end_slot = inp.readInt(True)
            v = self.read_vertices((flags & 16) != 0)
            color = inp.readInt() if nonessential else 0
            a["end"] = self.slot_names[end_slot]
            self._attach_vertex_info(a, v)
            if nonessential:
                a["color"] = _hex_rgba8888(color)
            return a
        return None  # 'sequence' type: Java returns null without reading anything further.

    # ------------------------------------------------------------- animations
    def _bezier(self, n_values):
        """Read n_values bezier control groups (cx1, cy1, cx2, cy2) as setBezier does."""
        inp = self.input
        out = []
        for _ in range(n_values):
            out.extend([_f(inp.readFloat()), _f(inp.readFloat()), _f(inp.readFloat()), _f(inp.readFloat())])
        return out

    def _curve(self, key, n_values):
        """Read the curve type byte used by most timelines and attach it to key."""
        c = self.input.readByte()
        if c == CURVE_STEPPED:
            key["curve"] = "stepped"
        elif c == CURVE_BEZIER:
            key["curve"] = self._bezier(n_values)

    def _read_curve_timeline(self, names):
        """readTimeline for CurveTimeline1 (names=('value',)) or CurveTimeline2 (names=(x, y))."""
        inp = self.input
        frame_count = self._frame_count
        keys = []
        key = {"time": _f(inp.readFloat())}
        for nm in names:
            key[nm] = _f(inp.readFloat())
        frame = 0
        while True:
            keys.append(key)
            if frame == frame_count - 1:
                break
            nkey = {"time": _f(inp.readFloat())}
            for nm in names:
                nkey[nm] = _f(inp.readFloat())
            self._curve(key, len(names))
            key = nkey
            frame += 1
        return keys

    def _read_color_timeline(self, n_bytes, fmt):
        """RGBA/RGB/RGBA2/RGB2/Alpha timelines: time float + n unsigned bytes per key."""
        inp = self.input
        frame_count = self._frame_count

        def read_key():
            k = {"time": _f(inp.readFloat())}
            vals = [inp.read() for _ in range(n_bytes)]
            fmt(k, vals)
            return k

        keys = []
        key = read_key()
        frame = 0
        while True:
            keys.append(key)
            if frame == frame_count - 1:
                break
            nkey = read_key()
            self._curve(key, n_bytes)
            key = nkey
            frame += 1
        return keys

    def read_animation(self):
        inp = self.input
        anim = {}
        inp.readInt(True)  # timelines array capacity hint (total timeline count)
        slot_names = self.slot_names
        bone_names = self.bone_names

        # Slot timelines.
        slots_tl = {}
        for _ in range(inp.readInt(True)):
            slot_index = inp.readInt(True)
            st = slots_tl.setdefault(slot_names[slot_index], {})
            for _ in range(inp.readInt(True)):
                tl_type = inp.readByte()
                frame_count = inp.readInt(True)
                self._frame_count = frame_count
                if tl_type == SLOT_ATTACHMENT:
                    st["attachment"] = [{"time": _f(inp.readFloat()), "name": inp.readStringRef()}
                                        for _ in range(frame_count)]
                    continue
                inp.readInt(True)  # bezierCount
                if tl_type == SLOT_RGBA:
                    st["rgba"] = self._read_color_timeline(
                        4, lambda k, v: k.__setitem__("color", "%02x%02x%02x%02x" % tuple(v)))
                elif tl_type == SLOT_RGB:
                    st["rgb"] = self._read_color_timeline(
                        3, lambda k, v: k.__setitem__("color", "%02x%02x%02x" % tuple(v)))
                elif tl_type == SLOT_RGBA2:
                    def f(k, v):
                        k["light"] = "%02x%02x%02x%02x" % tuple(v[:4])
                        k["dark"] = "%02x%02x%02x" % tuple(v[4:])
                    st["rgba2"] = self._read_color_timeline(7, f)
                elif tl_type == SLOT_RGB2:
                    def f2(k, v):
                        k["light"] = "%02x%02x%02x" % tuple(v[:3])
                        k["dark"] = "%02x%02x%02x" % tuple(v[3:])
                    st["rgb2"] = self._read_color_timeline(6, f2)
                elif tl_type == SLOT_ALPHA:
                    st["alpha"] = self._read_color_timeline(
                        1, lambda k, v: k.__setitem__("value", _f(v[0] / 255.0)))
                else:
                    raise SkelError("Unknown slot timeline type %d at %d" % (tl_type, inp.pos))
        if slots_tl:
            anim["slots"] = slots_tl

        # Bone timelines.
        bones_tl = {}
        for _ in range(inp.readInt(True)):
            bone_index = inp.readInt(True)
            bt = bones_tl.setdefault(bone_names[bone_index], {})
            for _ in range(inp.readInt(True)):
                tl_type = inp.readByte()
                frame_count = inp.readInt(True)
                self._frame_count = frame_count
                if tl_type == BONE_INHERIT:
                    bt["inherit"] = [{"time": _f(inp.readFloat()), "inherit": INHERIT[inp.readByte()]}
                                     for _ in range(frame_count)]
                    continue
                inp.readInt(True)  # bezierCount
                if tl_type not in BONE_TIMELINE_NAMES:
                    # Java's switch silently ignores unknown types, which would desync; fail loudly.
                    raise SkelError("Unknown bone timeline type %d at %d" % (tl_type, inp.pos))
                tname, vnames = BONE_TIMELINE_NAMES[tl_type]
                bt[tname] = self._read_curve_timeline(vnames)
        if bones_tl:
            anim["bones"] = bones_tl

        # IK constraint timelines.
        ik_tl = {}
        for _ in range(inp.readInt(True)):
            index = inp.readInt(True)
            frame_count = inp.readInt(True)
            inp.readInt(True)  # bezierCount
            keys = []

            def read_ik_key(flags):
                k = {"time": _f(inp.readFloat())}
                k["mix"] = (_f(inp.readFloat()) if flags & 2 else 1.0) if flags & 1 else 0.0
                k["softness"] = _f(inp.readFloat()) if flags & 4 else 0.0
                return k

            flags = inp.read()
            key = read_ik_key(flags)
            frame = 0
            while True:
                key["bendPositive"] = (flags & 8) != 0
                key["compress"] = (flags & 16) != 0
                key["stretch"] = (flags & 32) != 0
                keys.append(key)
                if frame == frame_count - 1:
                    break
                flags = inp.read()
                nkey = read_ik_key(flags)
                if flags & 64:
                    key["curve"] = "stepped"
                elif flags & 128:
                    key["curve"] = self._bezier(2)
                key = nkey
                frame += 1
            ik_tl.setdefault(self.ik_names[index], []).extend(keys)
        if ik_tl:
            anim["ik"] = ik_tl

        # Transform constraint timelines.
        tc_tl = {}
        names = ("mixRotate", "mixX", "mixY", "mixScaleX", "mixScaleY", "mixShearY")
        for _ in range(inp.readInt(True)):
            index = inp.readInt(True)
            self._frame_count = inp.readInt(True)
            inp.readInt(True)  # bezierCount
            tc_tl[self.tc_names[index]] = self._read_curve_timeline(names)
        if tc_tl:
            anim["transform"] = tc_tl

        # Path constraint timelines.
        pc_tl = {}
        for _ in range(inp.readInt(True)):
            index = inp.readInt(True)
            pt = pc_tl.setdefault(self.pc_names[index], {})
            for _ in range(inp.readInt(True)):
                tl_type = inp.readByte()
                self._frame_count = inp.readInt(True)
                inp.readInt(True)  # bezierCount
                if tl_type == PATH_POSITION:
                    pt["position"] = self._read_curve_timeline(("value",))
                elif tl_type == PATH_SPACING:
                    pt["spacing"] = self._read_curve_timeline(("value",))
                elif tl_type == PATH_MIX:
                    pt["mix"] = self._read_curve_timeline(("mixRotate", "mixX", "mixY"))
                else:
                    raise SkelError("Unknown path timeline type %d" % tl_type)
        if pc_tl:
            anim["path"] = pc_tl

        # Physics timelines.
        ph_tl = {}
        for _ in range(inp.readInt(True)):
            index = inp.readInt(True) - 1
            cname = "" if index == -1 else self.ph_names[index]  # "" = all physics constraints
            pt = ph_tl.setdefault(cname, {})
            for _ in range(inp.readInt(True)):
                tl_type = inp.readByte()
                frame_count = inp.readInt(True)
                self._frame_count = frame_count
                if tl_type == PHYSICS_RESET:
                    pt["reset"] = [{"time": _f(inp.readFloat())} for _ in range(frame_count)]
                    continue
                inp.readInt(True)  # bezierCount
                if tl_type not in PHYSICS_TIMELINE_NAMES:
                    raise SkelError("Unknown physics timeline type %d" % tl_type)
                pt[PHYSICS_TIMELINE_NAMES[tl_type]] = self._read_curve_timeline(("value",))
        if ph_tl:
            anim["physics"] = ph_tl

        # Attachment timelines (deform / sequence).
        att_tl = {}
        for _ in range(inp.readInt(True)):
            skin_index = inp.readInt(True)
            skin_name = self.skins[skin_index]["name"]
            skin_atts = self._skin_attachments[skin_index]
            for _ in range(inp.readInt(True)):
                slot_index = inp.readInt(True)
                for _ in range(inp.readInt(True)):
                    att_name = inp.readStringRef()
                    att = skin_atts.get((slot_index, att_name))
                    if att is None:
                        raise SkelError("Timeline attachment not found: " + str(att_name))
                    tl_type = inp.readByte()
                    frame_count = inp.readInt(True)
                    slot_d = att_tl.setdefault(skin_name, {}).setdefault(slot_names[slot_index], {})
                    ad = slot_d.setdefault(att_name, {})
                    if tl_type == ATTACHMENT_DEFORM:
                        weighted = att.get("_bones") is not None
                        vertices = att.get("_vertices")
                        if vertices is None:
                            raise SkelError("Deform on non-vertex attachment " + str(att_name))
                        deform_length = (len(vertices) // 3) << 1 if weighted else len(vertices)
                        inp.readInt(True)  # bezierCount
                        keys = []
                        time = _f(inp.readFloat())
                        frame = 0
                        while True:
                            key = {"time": time}
                            end = inp.readInt(True)
                            if end != 0:
                                start = inp.readInt(True)
                                key["offset"] = start
                                key["vertices"] = [_f(inp.readFloat()) for _ in range(end)]
                            # (end == 0 => setup pose / zero offsets)
                            key["deformLength"] = deform_length
                            keys.append(key)
                            if frame == frame_count - 1:
                                break
                            time2 = _f(inp.readFloat())
                            self._curve(key, 1)
                            time = time2
                            frame += 1
                        ad["deform"] = keys
                    elif tl_type == ATTACHMENT_SEQUENCE:
                        keys = []
                        for _ in range(frame_count):
                            t = _f(inp.readFloat())
                            mode_and_index = inp.readInt()
                            keys.append({"time": t, "mode": SEQUENCE_MODE[mode_and_index & 0xF],
                                         "index": mode_and_index >> 4, "delay": _f(inp.readFloat())})
                        ad["sequence"] = keys
                    else:
                        raise SkelError("Unknown attachment timeline type %d" % tl_type)
        if att_tl:
            anim["attachments"] = att_tl
            # Spine 4.0-style alias used by the requested structure.
            anim["deform"] = {sk: {sl: {an: d["deform"] for an, d in atts.items() if "deform" in d}
                                   for sl, atts in slots.items()}
                              for sk, slots in att_tl.items()}

        # Draw order timeline.
        draw_order_count = inp.readInt(True)
        if draw_order_count > 0:
            slot_count = len(slot_names)
            keys = []
            for _ in range(draw_order_count):
                time = _f(inp.readFloat())
                offset_count = inp.readInt(True)
                draw_order = [-1] * slot_count
                unchanged = [0] * (slot_count - offset_count)
                original_index = 0
                unchanged_index = 0
                offsets = []
                for _ in range(offset_count):
                    slot_index = inp.readInt(True)
                    while original_index != slot_index:
                        unchanged[unchanged_index] = original_index
                        unchanged_index += 1
                        original_index += 1
                    off = inp.readInt(True)
                    offsets.append({"slot": slot_names[slot_index], "offset": off})
                    draw_order[original_index + off] = original_index
                    original_index += 1
                while original_index < slot_count:
                    unchanged[unchanged_index] = original_index
                    unchanged_index += 1
                    original_index += 1
                for ii in range(slot_count - 1, -1, -1):
                    if draw_order[ii] == -1:
                        unchanged_index -= 1
                        draw_order[ii] = unchanged[unchanged_index]
                keys.append({"time": time, "offsets": offsets,
                             "order": [slot_names[i] for i in draw_order]})
            anim["drawOrder"] = keys

        # Event timeline.
        event_count = inp.readInt(True)
        if event_count > 0:
            keys = []
            for _ in range(event_count):
                time = _f(inp.readFloat())
                ename, edata = self.event_list[inp.readInt(True)]
                k = {"time": time, "name": ename, "int": inp.readInt(False), "float": _f(inp.readFloat())}
                s = inp.readString()
                k["string"] = s if s is not None else edata["string"]
                if edata["audio"] is not None:
                    k["volume"] = _f(inp.readFloat())
                    k["balance"] = _f(inp.readFloat())
                keys.append(k)
            anim["events"] = keys

        anim["duration"] = _f(self._duration(anim))
        return anim

    @staticmethod
    def _duration(anim):
        """Animation duration = max last-key time over all timelines (Timeline.getDuration)."""
        d = 0.0

        def walk(o):
            nonlocal d
            if isinstance(o, list):
                if o and isinstance(o[0], dict) and "time" in o[0]:
                    d = max(d, o[-1]["time"])
                return
            if isinstance(o, dict):
                for k, v in o.items():
                    if k == "deform" and isinstance(v, dict):
                        continue  # alias of "attachments"
                    walk(v)

        walk(anim)
        return d


def read_skel(path):
    """Parse a Spine 4.2 .skel file. Raises SkelError unless the reader ends exactly at EOF."""
    with open(path, "rb") as fh:
        data = fh.read()
    reader = SkeletonBinaryReader(data)
    out = reader.read()
    if reader.input.pos != len(data):
        raise SkelError("Reader stopped at byte %d of %d (not at EOF)" % (reader.input.pos, len(data)))
    return out


def main(argv):
    if len(argv) < 2:
        print("usage: skel_reader.py input.skel [output.json]", file=sys.stderr)
        return 2
    data = read_skel(argv[1])
    text = json.dumps(data, indent=1, ensure_ascii=False)
    if len(argv) > 2:
        with open(argv[2], "w", encoding="utf-8") as fh:
            fh.write(text)
        print("%s: %d bones, %d slots, %d skins, %d animations, %d/%d bytes -> %s" % (
            argv[1], len(data["bones"]), len(data["slots"]), len(data["skins"]), len(data["animations"]),
            data["_bytes_read"], data["_bytes_total"], argv[2]))
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
