using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class Grouping : MonoBehaviour
{
    /// <summary>
    /// 主Camera
    /// </summary>
    Camera Cam;
    /// <summary>
    /// Square Positions on Screen, [0] is Up-Right , [1] is Up-Left , [2] is Down-Left , [3] is Down-Right
    /// </summary>
    /// [1]--[0]
    ///  |    |
    ///  |    |
    /// [2]--[3]
    Vector3[] ScreenPositions = new Vector3[4];
    /// <summary>
    /// 滑鼠按下的點
    /// </summary>
    Vector3 MouseDownPosition;
    /// <summary>
    /// 滑鼠放開的點
    /// </summary>
    Vector3 MouseUpPosition;
    /// <summary>
    /// 視錐4個近點(在 NearClipPlane上)
    /// </summary>
    Vector3[] NearPositions = new Vector3[4];
    /// <summary>
    /// 視錐4個遠點(在 FarClipPlane 上)
    /// </summary>
    Vector3[] FarPositions = new Vector3[4];
    /// <summary>
    /// 視錐上的6個Plane, [0] = Left, [1] = Right, [2] = Down, [3] = Up, [4] = Near, [5] = Far
    /// </summary>
    Plane[] FrustumPlanes = new Plane[6];
    /// <summary>
    /// 要被Grouping的物件
    /// </summary>
    public Collider[] Objects;
    /// <summary>
    /// Grouping 物件的初始 Material
    /// </summary>
    public Material DefaultMaterial;
    /// <summary>
    /// Grouping 物件被選擇時的 Material
    /// </summary>
    public Material SelectedMaterial;
    /// <summary>
    /// Drag 時的 Outline
    /// </summary>
    public LineRenderer DragOutline;
    /// <summary>
    /// 設定 DragOutline 的顏色
    /// </summary>
    public Color OutlineColor { set { DragOutline.material.color = value; } }
    /// <summary>
    /// Drag 時填充 Outline 的 Image
    /// </summary>
    public RectTransform DragFill;
    /// <summary>
    /// 設定 DragFill 的顏色
    /// </summary>
    public Color FillColor { set { DragFill.GetComponent<Image>().color = value; } }
    void Start()
    {
        Cam = Camera.main;
        for (int i = 0; i < Objects.Length; i++) { Objects[i].GetComponent<MeshRenderer>().material = DefaultMaterial; }
    }


    void Update()
    {
        //按下左鍵時,紀錄按下時的螢幕位置
        if (Input.GetMouseButtonDown(0)) 
        {
            MouseDownPosition = Input.mousePosition;
            DragOutline.gameObject.SetActive(true);
            DragFill.gameObject.SetActive(true);
        }
        //持續按壓時，畫出外框
        if (Input.GetMouseButton(0)) 
        {
            //得出螢幕4點
            SetScreenRange(MouseDownPosition, Input.mousePosition);
            //用螢幕4點得出NearPlane上的4點
            SetPlanePosition(Cam.nearClipPlane, ref NearPositions);
            //用NearPlane上的4點畫出框來
            for (int i = 0; i < NearPositions.Length; i++) { DragOutline.SetPosition(i, NearPositions[i]); }
            //用NearPlane上的4點填滿框
            DragFill.anchoredPosition = Vector2.zero;
            for (int i = 0; i < ScreenPositions.Length; i++) 
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
            DragOutline.gameObject.SetActive(false);
            DragFill.gameObject.SetActive(false);
            //先Reset 所有方塊的Material
            for (int i = 0; i < Objects.Length; i++) { Objects[i].GetComponent<MeshRenderer>().material = DefaultMaterial; }
            MouseUpPosition = Input.mousePosition;

            //得出空間中8個錐體點，如果得不出就直接返回
            if (!SetFroupFrustum()) 
            {
                return;
            }
            //依照錐體定義的範圍，將範圍內的物體換成其他Material
            for (int i = 0; i < Objects.Length; i++) 
            {
                if (GeometryUtility.TestPlanesAABB(FrustumPlanes, Objects[i].bounds))
                {
                    Objects[i].GetComponent<MeshRenderer>().material = SelectedMaterial;
                }
            }

        }
        
    }
    /// <summary>
    /// 依據螢幕上的2點得出4邊形的4個點
    /// </summary>
    /// <param name="MouseDownPoint"></param>
    /// <param name="MouseUpPoint"></param>
    void SetScreenRange(Vector3 MouseDownPoint,Vector3 MouseUpPoint ) 
    {
        //如果 X 或 Y 一樣就返回
        //if (MouseDownPoint.x == MouseUpPoint.x || MouseDownPoint.y == MouseUpPoint.y) { return false; }

        float Max_X;
        float Min_X;
        float Max_Y;
        float Min_Y;
        if (MouseDownPoint.x > MouseUpPoint.x) { Max_X = MouseDownPoint.x; Min_X = MouseUpPoint.x;   }
        else                                   { Max_X = MouseUpPoint.x;   Min_X = MouseDownPoint.x; }
        if (MouseDownPoint.y > MouseUpPoint.y) { Max_Y = MouseDownPoint.y; Min_Y = MouseUpPoint.y;   }
        else                                   { Max_Y = MouseUpPoint.y;   Min_Y = MouseDownPoint.y; }
        ScreenPositions[0] = new Vector3(Max_X, Max_Y, 0);
        ScreenPositions[1] = new Vector3(Min_X, Max_Y, 0);
        ScreenPositions[2] = new Vector3(Min_X, Min_Y, 0);
        ScreenPositions[3] = new Vector3(Max_X, Min_Y, 0);
    }
    /// <summary>
    /// 將螢幕上4點轉為空間視錐的8點，如果成功轉化就回傳true，不成功(螢幕四點為一直線或集中在一起)就回傳false
    /// </summary>
    /// <returns></returns>
    bool SetFroupFrustum() 
    {
        SetScreenRange(MouseDownPosition, MouseUpPosition);
        //檢查螢幕4點是否集中在一起或一直線
        if (ScreenPositions[0] == ScreenPositions[1] || ScreenPositions[1] == ScreenPositions[2])
        {
            return false;
        }
        //由螢幕上4點，得出空間錐體的8個點
        SetPlanePosition(Cam.nearClipPlane, ref NearPositions);
        SetPlanePosition(Cam.farClipPlane,  ref FarPositions);
        
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
    /// <param name="PlaneDistance"></param>
    /// <param name="PlanePositions"></param>
    void SetPlanePosition(float PlaneDistance, ref Vector3[] PlanePositions)
    {
        for (int i = 0; i < ScreenPositions.Length; i++) 
        {
            ScreenPositions[i].z = PlaneDistance;
            PlanePositions[i] = Cam.ScreenToWorldPoint(ScreenPositions[i]);
        }
    }

}
