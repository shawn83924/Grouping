using System.Collections.Generic;
using Drawing;
using UnityEngine;

public class GroupingManager : MonoBehaviour
{
    private static GroupingManager _instance;
    public static GroupingManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GroupingManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("GroupingManager");
                    _instance = obj.AddComponent<GroupingManager>();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 主Camera
    /// </summary>
    private Camera Cam;
    /// <summary>
    /// Square Positions on Screen, [0] is Up-Right , [1] is Up-Left , [2] is Down-Left , [3] is Down-Right
    /// </summary>
    /// [1]--[0]
    ///  |    |
    ///  |    |
    /// [2]--[3]
    private Vector3[] ScreenPositions = new Vector3[4];
    /// <summary>
    /// 滑鼠按下的點
    /// </summary>
    private Vector3 MouseDownPosition;
    /// <summary>
    /// 滑鼠放開的點
    /// </summary>
    private Vector3 MouseUpPosition;
    /// <summary>
    /// 視錐4個近點(在 NearClipPlane上)
    /// </summary>
    private Vector3[] NearPositions = new Vector3[4];
    /// <summary>
    /// 視錐4個遠點(在 FarClipPlane 上)
    /// </summary>
    private Vector3[] FarPositions = new Vector3[4];
    /// <summary>
    /// 視錐上的6個Plane, [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
    /// </summary>
    private Plane[] FrustumPlanes = new Plane[6];
    [SerializeField]
    private Color outlineColor;
    [SerializeField]
    private float outlineThickness;
    /// <summary>
    /// 要被Grouping的物件
    /// </summary>
    [HideInInspector]
    public List<GroupingObj> Objects = new();
    /// <summary>
    /// Grouping 物件的初始 Material
    /// </summary>
    public Material DefaultMaterial;
    /// <summary>
    /// Grouping 物件被選擇時的 Material
    /// </summary>
    public Material SelectedMaterial;
    /// <summary>
    /// Drag 時填充 Outline 的 Image
    /// </summary>
    public RectTransform DragFill;

    private void Awake()
    {
        _instance = this;
    }

    private void Start()
    {
        Cam = Camera.main;
    }

    private void Update()
    {
        //按下左鍵時,紀錄按下時的螢幕位置
        if (Input.GetMouseButtonDown(0))
        {
            MouseDownPosition = Input.mousePosition;
            DragFill.gameObject.SetActive(true);
        }
        //持續按壓時，畫出外框
        if (Input.GetMouseButton(0))
        {
            //得出螢幕4點
            SetScreenRange(MouseDownPosition, Input.mousePosition);
            //用螢幕4點得出NearPlane上的4點
            SetPlanePosition(Cam.nearClipPlane, NearPositions);
            //用NearPlane上的4點畫出框來
            DrawNearRectOutline(ScreenPositions, outlineColor);
            /*
            for (var i = 0; i < NearPositions.Length; i++)
            {
                DragOutline.SetPosition(i, NearPositions[i]);
            }
            */
            //用NearPlane上的4點填滿框
            DragFill.anchoredPosition = Vector2.zero;
            for (var i = 0; i < ScreenPositions.Length; i++)
            {
                DragFill.anchoredPosition += new Vector2(ScreenPositions[i].x, ScreenPositions[i].y);
            }
            DragFill.anchoredPosition /= 4;
            DragFill.sizeDelta = new Vector2( ScreenPositions[0].x - ScreenPositions[1].x,
                                              ScreenPositions[1].y - ScreenPositions[2].y);

        }

        //左鍵放開時，記錄放開時的螢幕位置
        if (Input.GetMouseButtonUp(0))
        {
            DragFill.gameObject.SetActive(false);
            //先Reset 所有方塊的Material
            foreach (var obj in Objects)
            {
                obj.MeshRenderer.sharedMaterial = DefaultMaterial;
            }
            MouseUpPosition = Input.mousePosition;

            //得出空間中8個錐體點，如果得不出就直接返回
            if (!SetGroupFrustum())
            {
                return;
            }
            //依照錐體定義的範圍，將範圍內的物體換成其他Material
            foreach (var obj in Objects)
            {
                if (GeometryUtility.TestPlanesAABB(FrustumPlanes, obj.Collider.bounds))
                {
                    obj.MeshRenderer.sharedMaterial = SelectedMaterial;
                }
            }

        }
    }
    /// <summary>
    /// 依據螢幕上的2點得出4邊形的4個點
    /// </summary>
    /// <param name="mouseDownPoint"></param>
    /// <param name="mouseUpPoint"></param>
    void SetScreenRange(Vector3 mouseDownPoint, Vector3 mouseUpPoint)
    {
        //如果 X 或 Y 一樣就返回
        //if (MouseDownPoint.x == MouseUpPoint.x || MouseDownPoint.y == MouseUpPoint.y) { return false; }

        float maxX;
        float minX;
        float maxY;
        float minY;
        if (mouseDownPoint.x > mouseUpPoint.x) { maxX = mouseDownPoint.x; minX = mouseUpPoint.x;   }
        else                                   { maxX = mouseUpPoint.x;   minX = mouseDownPoint.x; }
        if (mouseDownPoint.y > mouseUpPoint.y) { maxY = mouseDownPoint.y; minY = mouseUpPoint.y;   }
        else                                   { maxY = mouseUpPoint.y;   minY = mouseDownPoint.y; }
        ScreenPositions[0] = new Vector3(maxX, maxY, 0);
        ScreenPositions[1] = new Vector3(minX, maxY, 0);
        ScreenPositions[2] = new Vector3(minX, minY, 0);
        ScreenPositions[3] = new Vector3(maxX, minY, 0);
    }
    /// <summary>
    /// 將螢幕上4點轉為空間視錐的8點，如果成功轉化就回傳true，不成功(螢幕四點為一直線或集中在一起)就回傳false
    /// </summary>
    /// <returns></returns>
    bool SetGroupFrustum()
    {
        SetScreenRange(MouseDownPosition, MouseUpPosition);
        //檢查螢幕4點是否集中在一起或一直線
        if (ScreenPositions[0] == ScreenPositions[1] || ScreenPositions[1] == ScreenPositions[2])
        {
            return false;
        }
        //由螢幕上4點，得出空間錐體的8個點
        SetPlanePosition(Cam.nearClipPlane, NearPositions);
        SetPlanePosition(Cam.farClipPlane, FarPositions);

        FrustumPlanes[0].Set3Points(NearPositions[1], FarPositions[1],  FarPositions[2] );
        FrustumPlanes[1].Set3Points(FarPositions[0],  NearPositions[0], NearPositions[3]);
        FrustumPlanes[2].Set3Points(NearPositions[3], NearPositions[2], FarPositions[3] );
        FrustumPlanes[3].Set3Points(NearPositions[1], NearPositions[0], FarPositions[0] );
        FrustumPlanes[4].Set3Points(NearPositions[0], NearPositions[1], NearPositions[2]);
        FrustumPlanes[5].Set3Points(FarPositions[0],  FarPositions[3],  FarPositions[2] );
        //得出空間中錐體後就回傳True
        return true;
    }
    /// <summary>
    /// 用螢幕4點來設定 NearPlane 或 FarPlane
    /// </summary>
    /// <param name="planeDistance"></param>
    /// <param name="planePositions"></param>
    void SetPlanePosition(float planeDistance, Vector3[] planePositions)
    {
        for (var i = 0; i < ScreenPositions.Length; i++)
        {
            ScreenPositions[i].z = planeDistance;
            planePositions[i] = Cam.ScreenToWorldPoint(ScreenPositions[i]);
        }
    }

    private void DrawNearRectOutline(Vector3[] positions, Color color)
    {
        var draw = Draw.ingame;
        using (draw.InScreenSpace(Cam))
        {
            using (draw.WithLineWidth(outlineThickness))
            {
                var rectx = positions[1].x;
                var recty = positions[2].y;
                var rectwidth = positions[0].x - positions[1].x;
                var rectheight = positions[0].y - positions[3].y;
                draw.xy.WireRectangle(
                    new Rect(rectx, recty, rectwidth, rectheight),
                    color);
            }
        }
    }
}