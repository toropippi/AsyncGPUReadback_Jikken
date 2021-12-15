using UnityEngine;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;

//本来https://github.com/toropippi/GPUInstanceShadowsで
//影付きGPUInstanceの実験をするためのコードだった

//その後ある事情でGetDataとAsyncGPUReadback.Requestを使ったコードでGPU→CPU転送の同期、非同期の影響がフレームレートにどう影響するか気になり
//検証するために作った。

//結果は
//①同期GetData前つき27 .3fps
//②非同期29 .2fps
//③同期GetData後ろつき27 .3fps
//④通信なし29 .5fps
//結論AsyncGPUReadback.Requestで7%程度高速化

//ベースのGPU負荷はGPU計算75%、レンダリング25%
//使用GPUはRTX2060


public struct Point
{
    public Vector3 vertex;
    public Vector3 normal;
    public Vector2 uv;
    public Vector4 color;
}

public class CubeInstanceSample : MonoBehaviour
{
    Material material;
    public Shader shader;
    public ComputeShader CSshader;

    ComputeBuffer verticesBuffer;
    ComputeBuffer posBuffer,tesubude;

    CommandBuffer camera_command_buffer;
    CommandBuffer light_command_buffer;
    public Camera camera_source;
    public Light light_source;

    int CSMainkernel;
    int cnt;
    int nVerticesPerCube = 36; //its just like that.
    int CubeNum = 65536 * 4;//16;//must be 64x


    Vector3[] vertices = {
            new Vector3 (0, 0, 0), //0
            new Vector3 (1, 0, 0), //1
            new Vector3 (1, 1, 0), //2
            new Vector3 (0, 1, 0), //3
            new Vector3 (0, 1, 1), //4
            new Vector3 (1, 1, 1), //5
            new Vector3 (1, 0, 1), //6
            new Vector3 (0, 0, 1), //7
        };
    Vector3[] uvs = {
            new Vector2 (0, 0),
            new Vector2 (1, 1),
            new Vector2 (1, 0),
            new Vector2 (0, 0),
            new Vector2 (0, 1),
            new Vector2 (1, 1)
        };
    int[] triangles = {
            0, 2, 1, //face front
			0, 3, 2,
            3, 5, 2, //face top
			3, 4, 5,
            1, 5, 6, //face right
			1, 2, 5,
            7, 3, 0, //face left
			7, 4, 3,
            6, 4, 7, //face back
			6, 5, 4,
            7, 1, 6, //face bottom
			7, 0, 1
        };
    Color[] cols =
    {
            Color.red,
            Color.green,
            Color.blue,
            Color.red,
            Color.white,
            Color.green
    };

    //just a function to get a all the data for one cube at once
    public void GetCube(out Vector3[] myVertices, out Vector3[] myNormals, out Vector2[] myUVs, out Color[] myCols)
    {
        Vector3[] v = new Vector3[triangles.Length];
        Vector2[] u = new Vector2[triangles.Length];
        Vector3[] n = new Vector3[triangles.Length];
        Color[] c = new Color[triangles.Length];
        for (int i = 0; i < triangles.Length; i++)
        {
            v[i] = vertices[triangles[i]];
            u[i] = uvs[i%uvs.Length];
            c[i] = cols[i % cols.Length];
        }


        for (int i = 0; i<vertices.Length; i++)
        {
            Vector3 side1 = vertices[triangles[i * 3 + 1]] - vertices[triangles[i * 3 + 0]];
            Vector3 side2 = vertices[triangles[i * 3 + 2]] - vertices[triangles[i * 3 + 0]];
            Vector3 side3 = vertices[triangles[i * 3 + 2]] - vertices[triangles[i * 3 + 1]];
            Vector3 perp1 = Vector3.Cross(side1, side2);
            Vector3 perp2 = Vector3.Cross(side1, side3);
            Vector3 perp3 = Vector3.Cross(side2, side3);
            perp1 /= perp1.magnitude;
            perp2 /= perp2.magnitude;
            perp3 /= perp3.magnitude;
            n[i * 3 + 0] = perp1;
            n[i * 3 + 1] = perp2;
            n[i * 3 + 2] = perp3;
        }
        myVertices = v;
        myUVs = u;
        myCols = c;
        myNormals = n;
    }


    //1つのbox→全部の頂点、法線など計算して配列に格納
    void allVerticesInit(ref Point[] allVertices)
    {

        Vector3 cubeScale = new Vector3(0.08f, 0.08f, 0.08f);

        //iterating through each cubeposition with resX & resY aka columns/rows
        for (int j = 0; j < CubeNum; j++)
        {
            int cubeCounter = j * nVerticesPerCube;
            Vector3 posOffset = new Vector3(0f,0f,0f);

            //getting cube data
            GetCube(out Vector3[] verts, out Vector3[] norms, out Vector2[] uvvs, out Color[] colz);

            //iterating through the data we got from the function above and write it into allVertices array.
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 vp = new Vector3(verts[i].x * cubeScale.x, verts[i].y * cubeScale.y, verts[i].z * cubeScale.z);
                allVertices[cubeCounter + i].vertex = vp + posOffset;
                allVertices[cubeCounter + i].uv = uvvs[i];
                allVertices[cubeCounter + i].normal = norms[i];
                allVertices[cubeCounter + i].color = colz[i];
            }
        }
    }


    private void Start()
    {
        int nVertices = nVerticesPerCube * CubeNum;
        material = new Material(shader);
        CSMainkernel = CSshader.FindKernel("CSMain");
        
        Point[] allVertices = new Point[nVertices];
        allVerticesInit(ref allVertices);
        
        //writing verticesBuffer to material/shader
        verticesBuffer = new ComputeBuffer(allVertices.Length, Marshal.SizeOf(allVertices.GetType().GetElementType()));
        verticesBuffer.SetData(allVertices);

        tesubude = new ComputeBuffer(16, 4);

        posBuffer = new ComputeBuffer(CubeNum * 3, 4);//x,y,z
        CSshader.SetBuffer(CSMainkernel, "posbuf", posBuffer);

        material.SetBuffer("posbuf", posBuffer);
        material.SetBuffer("points", verticesBuffer);

        //tell renderenging when to draw geometry
        camera_command_buffer = new CommandBuffer();
        camera_command_buffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, nVertices);
        camera_source.AddCommandBuffer(CameraEvent.BeforeGBuffer, camera_command_buffer);

        //tell renderengine when to draw geometry for shadow pass
        light_command_buffer = new CommandBuffer();
        light_command_buffer.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, nVertices);
        light_source.AddCommandBuffer(LightEvent.BeforeShadowMapPass, light_command_buffer);

        cnt = 0;
    }












    private void Update()
    {
        //①前つきのパターン
        //int[] w = new int[16];
        //posBuffer.GetData(w, 0, 0, 16);

        CSshader.SetFloat("time", 0.01238904567890f * cnt);
        CSshader.Dispatch(CSMainkernel, CubeNum / 64, 1, 1);


        //②Async使ったパターン
        /*
        AsyncGPUReadback.Request(posBuffer, 4 * 16, 0, request =>
          {
              if (request.hasError)
              {
                // エラー
                Debug.LogError("Error.");
              }
              else
              {
                // gpuに記憶されているデータを非同期に取得
                var data = request.GetData<uint>();
                  if (data[0] == 0)
                      Debug.Log(data[0] + "," + data[1] + "," + data[2]);
              }
          });
        */

        //③後ろつきのパターン
        //int[] w = new int[16];
        //posBuffer.GetData(w, 0, 0, 16);


        cnt++;
        cnt %= 1000;
    }










    private void OnDestroy()
    {
        verticesBuffer.Release();
        posBuffer.Release();
    }
}
