/// <summary>
/// Gerador de terreno procedural na unity
/// Cria um item no menu e uma janela no editor da unity
/// Código desenvolvido por Tomás Pedron Turchi Pacheco
/// </summary>
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal;
using UnityEditor.VersionControl;
using UnityEditor.PackageManager;
using System.Collections.Concurrent;
//TODO @tantofaz186 teste de commit 
public class GeradorDeTerreno : EditorWindow
{
    #region Cria a janela no editor
    [MenuItem("Mesh/Gerar Mesh Procedural")]
    static void Init()
    {
        GetWindow<GeradorDeTerreno>().Show();
    }
    #endregion
    #region Variaveis e Implementação da GUI

    public static MeshFilter meshFilter;
    static MeshRenderer renderer;
    static Material materialWireFrame
    {
        get
        {
            string path = "Assets/Terreno/Materiais/PlaneWireFrame.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("VR/SpatialMapping/Wireframe"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
    static Material materialColor
    {
        get
        {
            string path = "Assets/Terreno/Materiais/PlaneColor.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Particles/Standard Surface"));
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
    public static Texture2D topTexture;
    public static Texture2D middleTexture;
    public static Texture2D bottomTexture;


    static string[] shading = new string[] { "Smooth", "Flat" };
    static int shadingIndex = 0;
    static bool FlatShading //retorna true se o usuário escolheu Flat shading e false se o usuário escolheu Smooth shading
    {
        get { return shadingIndex == 1 ? true : false; }
    }

    static string[] resoluçãoDoMapaSmooth = { "2x2", "4x4", "8x8", "16x16", "32x32", "64x64", "128x128", "256x256" };
    static string[] resoluçãoDoMapaFlat = { "2x2", "4x4", "8x8", "16x16", "32x32", "64x64" };
    static string[] ResoluçãoDoMapa
    {
        get { return FlatShading ? resoluçãoDoMapaFlat : resoluçãoDoMapaSmooth; }
    }
    private static int _res = ResoluçãoDoMapa.Length - 1;
    static int resoluçãoDoMapaIndex
    {
        get
        {
            if (_res >= ResoluçãoDoMapa.Length)
                _res = ResoluçãoDoMapa.Length - 1;
            return _res;
        }
        set { _res = value; }
    }


    static float MultiplicadorDeNoise = 0;
    static bool randomizarNoise = false;
    static int oitavas = 1;
    static float alturaMaxima = 100;
    static float EscalaDoMapa = 1;



    static string[] colorTypes = new string[] { "WireFrame", "Color", "Texture", "None" };
    static int colorTypesIndex = 1;



    public void OnGUI()
    {
        shadingIndex = EditorGUILayout.Popup("Shading type", shadingIndex, shading);

        resoluçãoDoMapaIndex = EditorGUILayout.Popup("Resolução do Mapa", resoluçãoDoMapaIndex, ResoluçãoDoMapa);
        EscalaDoMapa = EditorGUILayout.FloatField("Escala do Mapa", EscalaDoMapa);
        alturaMaxima = EditorGUILayout.FloatField("Altura Máxima", alturaMaxima);
        oitavas = EditorGUILayout.IntSlider("Oitavas", oitavas, 1, 8);
        MultiplicadorDeNoise = EditorGUILayout.Slider("Multiplicador de Noise", MultiplicadorDeNoise, 0f, 100f);
        randomizarNoise = EditorGUILayout.Toggle("Randomizar Noise?", randomizarNoise);



        colorTypesIndex = EditorGUILayout.Popup("Color type", colorTypesIndex, colorTypes);
        if (colorTypesIndex == 2)
        {
            topTexture = (Texture2D)EditorGUILayout.ObjectField("Top Texture", topTexture, typeof(Texture2D), false);
            middleTexture = (Texture2D)EditorGUILayout.ObjectField("Middle Texture", middleTexture, typeof(Texture2D), false);
            bottomTexture = (Texture2D)EditorGUILayout.ObjectField("Bottom Texture", bottomTexture, typeof(Texture2D), false);
        }
        //--------------------------------------------------------------------------------------//
        if (meshFilter == null)
            meshFilter = GameObject.Find("Plane").GetComponent<MeshFilter>();

        //--------------------------------------------------------------------------------------//
        meshFilter = (MeshFilter)EditorGUILayout.ObjectField(meshFilter, typeof(MeshFilter), true);
        renderer = meshFilter.gameObject.GetComponent<MeshRenderer>();


        if (GUILayout.Button("Criar Malha"))
            DadosDaMalha.CriarMalha();
    }
    #endregion
    static class DadosDaMalha
    {
        static int[] triangulos;
        static float[,] noiseMap;
        static Vector3[] vertices;
        static Vector3[] normais;
        static Vector2[] UVs;
        static Color[] cores;
        public static int dimensãoDoMapaX;
        public static int dimensãoDoMapaZ;
        public static void CriarMalha()
        {
            Debug.Log("Comecei a criação da malha");
            SetarValores();
            LimparMeshAntiga();
            MeshDataCreation();
            DataToMesh();
            Debug.Log("Terminei de criar a malha");
        }
        static void SetarValores()
        {
            dimensãoDoMapaX = dimensãoDoMapaZ = (int)Mathf.Pow(2, resoluçãoDoMapaIndex + 1);
        }
        static void LimparMeshAntiga()
        {
            meshFilter.mesh.Clear();
        }
        static void DataToMesh()
        {
            Debug.Log("Vertices: " + vertices.Length);
            Debug.Log("Triangulos: " + triangulos.Length);
            Debug.Log("Normais: " + normais.Length);
            Debug.Log("UVs: " + UVs.Length);
            Debug.Log("Cores: " + cores.Length);
            meshFilter.mesh.vertices = vertices;
            meshFilter.mesh.triangles = triangulos;
            meshFilter.mesh.normals = normais;
            meshFilter.mesh.uv = UVs;
            meshFilter.mesh.colors = cores;
        }
        static void MeshDataCreation()
        {
            vertices = CalcularVertices();
            triangulos = CalcularTriangulos();
            UVs = CalcularUVs();
            cores = CalcularGradiente(noiseMap);
            renderer.material = materialColor;

            switch (colorTypesIndex)
            {
                case 0://wireframe
                    renderer.material = materialWireFrame;
                    break;
                case 1: //vertex color
                    cores = CalcularCores(cores);
                    break;
                case 2: //material color
                    cores = AplicarCorDoMaterial(cores);
                    break;
                default: //none
                    break;
            }

            if (FlatShading)
                Flatten();
            normais = CalcularNormais();
        }
        static Vector3[] CalcularVertices()
        {
            noiseMap = NoiseClass.GerarNoise(dimensãoDoMapaX, dimensãoDoMapaZ, MultiplicadorDeNoise, randomizarNoise, oitavas);
            List<Vector3> listaV = new List<Vector3>();
            for (int z = 0; z < dimensãoDoMapaZ; z++)
            {
                for (int x = 0; x < dimensãoDoMapaX; x++)
                {
                    listaV.Add(new Vector3(x * EscalaDoMapa, noiseMap[x, z] * alturaMaxima, z * EscalaDoMapa));
                }
            }
            return listaV.ToArray();
        }
        static int[] CalcularTriangulos()
        {
            List<int> listT = new List<int>();
            for (int vertice = 0, z = 0; z < dimensãoDoMapaZ - 1; z++, vertice++)
            {
                for (int x = 0; x < dimensãoDoMapaX - 1; x++, vertice++)
                {
                    listT.Add(vertice + 0);
                    listT.Add(vertice + dimensãoDoMapaX + 1);
                    listT.Add(vertice + 1);

                    listT.Add(vertice + 0);
                    listT.Add(vertice + dimensãoDoMapaX + 0);
                    listT.Add(vertice + dimensãoDoMapaX + 1);
                }
            }
            return listT.ToArray();
        }
        static Vector3[] CalcularNormais()
        {
            //Dado um triangulo de vértices ABC, a normal é o produto vetorial de (B-A x C-A), como mostra o exemplo abaixo.
            //normals[A] = Vector3.Cross(vertices[B] - vertices[A], vertices[C] - vertices[A]));

            Vector3[] normals = new Vector3[vertices.Length];
            for (int i = 0; i < triangulos.Length; i += 3)
            {
                int verticeA = triangulos[i + 0];
                int verticeB = triangulos[i + 1];
                int verticeC = triangulos[i + 2];

                Vector3 A = vertices[verticeA];
                Vector3 B = vertices[verticeB];
                Vector3 C = vertices[verticeC];

                Vector3 normal = Vector3.Cross(B - A, C - A).normalized;

                normals[verticeA] += normal;
                normals[verticeB] += normal;
                normals[verticeC] += normal;
            }
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i].Normalize();
            }
            return normals;
        }
        static Vector2[] CalcularUVs()
        {
            List<Vector2> listUV = new List<Vector2>();
            for (int i = 0; i < vertices.Length; i++)
            {
                listUV.Add(new Vector2(vertices[i].x / ((float)dimensãoDoMapaX * EscalaDoMapa), vertices[i].z / ((float)dimensãoDoMapaZ) * EscalaDoMapa));
            }
            return listUV.ToArray();
        }
        static Color[] CalcularGradiente(float[,] noise)
        {
            Texture2D texturaNoise = new Texture2D(dimensãoDoMapaX, dimensãoDoMapaZ);
            List<Color> listaCores = new List<Color>();
            for (int z = 0; z < dimensãoDoMapaZ; z++)
            {
                for (int x = 0; x < dimensãoDoMapaX; x++)
                {
                    float gradiente = noise[x, z];
                    listaCores.Add(new Color(gradiente, gradiente, gradiente, 0));
                }
            }
            texturaNoise.SetPixels(listaCores.ToArray());
            texturaNoise.Apply();
            byte[] texturaNoiseInformação = texturaNoise.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/Terreno/Texturas Procedurais/Mapa Perlin Noise.png", texturaNoiseInformação);
            return listaCores.ToArray();
        }
        static Color[] CalcularCores(Color[] vertexColor)
        {
            Texture2D texturaVertexColor = new Texture2D(dimensãoDoMapaX, dimensãoDoMapaZ);
            List<Color> listaCoresMaterial = new List<Color>();
            const float limite1 = 0.2f;
            const float limite2 = 0.4f;
            const float limite3 = 0.6f;
            const float limite4 = 0.8f;
            for (int z = 0; z < dimensãoDoMapaZ; z++)
            {
                for (int x = 0; x < dimensãoDoMapaX; x++)
                {
                    float gradiente = vertexColor[x + z * dimensãoDoMapaX].r;

                    if (gradiente > limite4)
                    {
                        listaCoresMaterial.Add(Color.red);
                    }
                    else if (gradiente > limite3)
                    {
                        listaCoresMaterial.Add(Color.Lerp(Color.green, Color.red, (gradiente - limite3) * 5f));
                    }
                    else if (gradiente > limite2)
                    {
                        listaCoresMaterial.Add(Color.green);
                    }
                    else if (gradiente > limite1)
                    {
                        listaCoresMaterial.Add(Color.Lerp(Color.blue, Color.green, (gradiente - limite1) * 5f));
                    }
                    else
                    {
                        listaCoresMaterial.Add(Color.blue);
                    }
                }
            }
            texturaVertexColor.SetPixels(listaCoresMaterial.ToArray());
            texturaVertexColor.Apply();
            byte[] texturaVertexColorInformação = texturaVertexColor.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/Terreno/Texturas Procedurais/Vertex Color.png", texturaVertexColorInformação);
            return listaCoresMaterial.ToArray();
        }
        static Color[] AplicarCorDoMaterial(Color[] vertexColor)
        {
            Texture2D texturaMaterial = new Texture2D(dimensãoDoMapaX, dimensãoDoMapaZ);
            List<Color> listaCoresMaterial = new List<Color>();
            const float limite1 = 0.2f;
            const float limite2 = 0.4f;
            const float limite3 = 0.6f;
            const float limite4 = 0.8f;
            for (int z = 0; z < dimensãoDoMapaZ; z++)
            {
                for (int x = 0; x < dimensãoDoMapaX; x++)
                {
                    int index = x + z * dimensãoDoMapaZ;
                    float gradiente = vertexColor[index].r;
                    if (gradiente > limite4)
                    {
                        listaCoresMaterial.Add(topTexture.GetPixel(x, z));
                    }
                    else if (gradiente > limite3)
                    {
                        listaCoresMaterial.Add(Color.Lerp(middleTexture.GetPixel(x, z), topTexture.GetPixel(x, z), (gradiente - limite3) * 5f));
                    }
                    else if (gradiente > limite2)
                    {
                        listaCoresMaterial.Add(middleTexture.GetPixel(x, z));
                    }
                    else if (gradiente > limite1)
                    {
                        listaCoresMaterial.Add(Color.Lerp(bottomTexture.GetPixel(x, z), middleTexture.GetPixel(x, z), (gradiente - limite1) * 5f));
                    }
                    else
                    {

                        listaCoresMaterial.Add(bottomTexture.GetPixel(x, z));
                    }
                }
            }
            texturaMaterial.SetPixels(listaCoresMaterial.ToArray());
            texturaMaterial.Apply();
            byte[] texturaMaterialInformação = texturaMaterial.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + "/Terreno/Texturas Procedurais/Textura Material.png", texturaMaterialInformação);
            return listaCoresMaterial.ToArray();
        }
        static void Flatten()
        {
            int[] triangles = triangulos;
            Vector3[] newVertices = new Vector3[triangles.Length];
            Vector2[] newUVs = new Vector2[triangles.Length];
            Color[] newColors = new Color[triangles.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                newVertices[i] = vertices[triangles[i]];
                newColors[i] = cores[triangles[i]];
                newUVs[i] = UVs[triangles[i]];
                triangles[i] = i;
            }
            vertices = newVertices;
            triangulos = triangles;
            UVs = newUVs;
            cores = newColors;
        }
    }
}
public static class NoiseClass
{
    static System.Random random = new System.Random();
    static float randomX, randomZ = 0;
    public static float[,] GerarNoise(int tamanhoX, int TamanhoZ, float noiseMultiplier, bool isRandom, int oitavas)
    {
        noiseMultiplier /= 1000;
        if (isRandom)
        {
            randomX += (float)random.NextDouble() * 10;
            randomZ += (float)random.NextDouble() * 10;
        }
        else
        {
            randomX = randomZ = 0;
        }
        float[,] noiseMap = new float[TamanhoZ, tamanhoX];
        for (int Z = 0; Z < TamanhoZ; Z++)
        {
            for (int X = 0; X < tamanhoX; X++)
            {
                float aux = 1;
                float noiseValue = 0;
                for (int i = 0; i < oitavas; i++)
                {
                    float noiseX = (randomX + X) * noiseMultiplier * aux;
                    float noiseZ = (randomZ + Z) * noiseMultiplier * aux;
                    noiseValue += Mathf.PerlinNoise(noiseX, noiseZ) / aux;
                    aux *= 2;
                }
                noiseValue *= noiseValue;
                noiseMap[Z, X] = noiseValue;
            }
        }
        return noiseMap;
    }
}
