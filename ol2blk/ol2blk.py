import sys
import math
import json

def recenter(uvs):
    # shift center of UVs to be in the (0,1) square
    result = []
    center_u = 0
    center_v = 0
    for uv in uvs:
        center_u = center_u + uv["u"]
        center_v = center_v + uv["v"]
    center_u = center_u / len(uvs)
    center_v = center_v / len(uvs)
    
    translate_u = - math.floor(center_u)
    translate_v = - math.floor(center_v)
    for uv in uvs:
        result.append({ "u": uv["u"] + translate_u, "v": uv["v"] + translate_v })
    return result

def scaleUV(u):
    return math.floor(u * 2048)
	
def scaleXYZ(x):
    return math.floor(x * 65535 * 5)
	
# OLE format:
# { properties, global_data, custom_level_info, verts, segments, entities }    
# verts: { "index": { x: float, y: float, z: float, marked: boolean } ... }
# segments: { "index": { marked, patfinding, exitsegment, dark, verts: [ index ... ], sides: [ side ... ], neighbors: [ index ... ] } }
# side: { marked, chunk_plane_order, tex_name, deformation_preset, deformation_height, verts: [index ...], uvs: [ {u, v} ... ], decals [ decal ... ], door }

def convert(in_filename):
    in_json = None
    with open(in_filename, 'r') as f:
        in_json = json.load(f)
    
    out_filename = in_filename + ".blk"
    with open(out_filename, 'w') as f2:
        f2.write("DMB_BLOCK_FILE\n")
        for segment_index, segment in in_json["segments"].items():
            f2.write("segment " + segment_index + "\n")
            for side_index, side in enumerate(segment["sides"]):
                f2.write("  " + "side " + str(side_index) + "\n")
                f2.write("    " + "tmap_num 0\n")
                f2.write("    " + "tmap_num2 0\n")
                for uv in recenter(side["uvs"]):
                    # set light value to a fixed constant
                    f2.write("    " + "uvls " + str(scaleUV(uv["u"])) + " " + str(scaleUV(uv["v"])) + " 2048\n")
            f2.write("  children")
            for neighbour in segment["neighbors"]:
                f2.write(" " + str(neighbour))
            f2.write("\n")
            for local_index, vert_index in enumerate(segment["verts"]):
                vert = in_json["verts"][str(vert_index)]
                f2.write("  vms_vector {0} {1} {2} {3}\n".format(local_index, scaleXYZ(vert["x"]), scaleXYZ(vert["y"]), scaleXYZ(vert["z"])))
            f2.write("  static_light 0\n")
    return out_filename
    
def __main__():
    if len(sys.argv) < 2:
        print("usage: ol2blk <.overload file>\n")
        return
    in_filename = sys.argv[1]
    print("Converting " + in_filename)
    try:
        out_filename = convert(in_filename)
        print("Converted " + in_filename + " to " + out_filename)
    except:
        print("error:", sys.exc_info()[0])
    
__main__()