
from .singleton import Singleton
import io
import bpy
from mathutils import Matrix
import json


class SceneObject(object):
    """
    Data object representing a geometric object in the blender scene
    """
    def __init__(self, name, vertices, normals, faces, face_materials, uvs, materials):
        self.name = name
        self.vertices = vertices
        self.normals = normals
        self.faces = faces
        self.face_materials = face_materials
        self.uvs = uvs
        self.materials = materials

    def to_dict(self):
        return {
            'name': self.name,
            'vertices': self.vertices,
            'normals': self.normals,
            'faces': self.faces,
            'face_materials': self.face_materials,
            'uvs': self.uvs,
            'materials': self.materials
        }

    def to_json(self):
        return json.dumps(self.to_dict())


class SceneObjectParser:
    """
    Serailise a object from a Blender scene
    """

    @staticmethod
    def face_to_triangle(face):
        """
        convert poly to a tri
        :param face:
        :return:
        """
        triangles = []

        if len(face) == 4:
            triangles.append([face[0], face[1], face[2]])
            triangles.append([face[2], face[3], face[0]])
        else:
            triangles.append(face)

        return triangles

    @staticmethod
    def get_normals_from_vertices_array(vertices):
        return [vert.normal[:] for vert in vertices]

    @staticmethod
    def get_positions_from_vertices_array(vertices, matrix=None):
        if not matrix:
            matrix = Matrix()
            Matrix.identity(matrix)

        return [(matrix * vert.co)[:] for vert in vertices]

    @staticmethod
    def get_indices_from_faces_array(faces):
        indices = []

        for face in faces:
            if len(face.vertices) == 3:
                indices.extend(face.vertices)
            else:
                indices.extend(face.vertices)
                indices.extend([face.vertices[0], face.vertices[2], face.vertices[3]])

        return indices

    @staticmethod
    def get_material_indices_from_faces_array(faces):
        indices = []

        for face in faces:
            indices.append(face.material_index)

        return indices

    @staticmethod
    def get_materials_from_materials_array(face_materials):
        """
        API reference:
        https://www.blender.org/api/blender_python_api_2_67_release//bpy.types.Material.html
        https://wiki.blender.org/index.php/Dev:Py/Scripts/Cookbook/Code_snippets/Materials_and_textures
        """
        materials = []

        for face_material in face_materials:
            mat = {
                "name": face_material.name,
                "diffuse_color": [face_material.diffuse_color[0], face_material.diffuse_color[1],
                                  face_material.diffuse_color[2]],
                "diffuse_shader": face_material.diffuse_shader,
                "diffuse_shader": face_material.diffuse_shader,
                "diffuse_intensity": face_material.diffuse_intensity,
                "specular_color": [face_material.specular_color[0], face_material.specular_color[1],
                                   face_material.specular_color[2]],
                "specular_shader": face_material.specular_shader,
                "specular_intensity": face_material.specular_intensity,
                "alpha": face_material.alpha,
                "ambient": face_material.ambient
            }

            materials.append(mat)

        return materials

    @staticmethod
    def serailise_scene_object(obj):
        """
        API reference:
        https://docs.blender.org/api/blender_python_api_2_63_2/bpy.types.Mesh.html
        :param obj: Blender scene object
        :return: serialised blender object
        """
        matrix = obj.matrix_world.copy()

        obj.data.calc_tessface()
        faces = obj.data.tessfaces
        vertices = obj.data.vertices
        facesMaterials = obj.data.materials

        if obj.data.tessface_uv_textures.active is not None:
            facesuvs = obj.data.tessface_uv_textures.active.data
        else:
            facesuvs = []

        so = SceneObject(
            name=obj.name,
            vertices=SceneObjectParser.get_positions_from_vertices_array(vertices, matrix),
            normals=SceneObjectParser.get_normals_from_vertices_array(vertices),
            faces=SceneObjectParser.get_indices_from_faces_array(faces),
            face_materials=SceneObjectParser.get_material_indices_from_faces_array(faces),
            uvs=facesuvs,
            materials=SceneObjectParser.get_materials_from_materials_array(facesMaterials)
        )

        return so