using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///  ласс содержит всю инф дл€ замены плитки: индекс особой плитки, 
/// префаб, дроп, индекс плитки дл€ замены
/// </summary>
[System.Serializable]
public class TileSwap
{
    public int tileNum;
    public GameObject swapPrefab;
    public GameObject guaranteedItemDrop;
    public int overrideTileNum = -1;
}
public class TileCamera : MonoBehaviour
{
    static private int W, H;
    static private int[,] MAP;
    static public Sprite[] SPRITES;
    static public Transform TILE_ANCHOR;
    static public Tile[,] TILES;
    static public string COLLISIONS;

    [Header("Set in Inspector")]
    public TextAsset mapData;
    public Texture2D mapTiles;
    public TextAsset mapCollisions;
    public Tile tilePrefab;
    public int defaultTileNum;
    public List<TileSwap> tileSwaps;//<-информаци€ о заменах вводитс€ сюда. после преобр. в словарь

    private Dictionary<int, TileSwap> tileSwapDict;
    private Transform enemyAnchor, itemAnchor;

    private void Awake()
    {
        COLLISIONS = Utils.RemoveLineEndings(mapCollisions.text);
        PrepareTileSwapDict();
        enemyAnchor = (new GameObject("Enemy Anchor")).transform;
        itemAnchor = (new GameObject("Item Anchor")).transform;
        LoadMap(); 
    }
    public void LoadMap()
    {
        //создание €кор€-родител€ дл€ всех тайлов
        GameObject go = new GameObject("TILE_ANCHOR");
        TILE_ANCHOR = go.transform;

        //загрузка спрайтов тайлов
        SPRITES = Resources.LoadAll<Sprite>(mapTiles.name);

        //считывание размеров карты
        string[] lines = mapData.text.Split('\n');
        H = lines.Length;
        string[] tileNums = lines[0].Split(' ');
        W = tileNums.Length;

        //сохранение информации дл€ карты в двумерный массив дл€ ускорени€ доступа
        System.Globalization.NumberStyles hexNum;
        hexNum = System.Globalization.NumberStyles.HexNumber;
        MAP = new int[W, H];
        for(int j = 0; j < H; j++)
        {
            tileNums = lines[j].Split(' ');
            for (int i = 0; i < W; i++)
            {
                if (tileNums[i] == "..")
                    MAP[i, j] = 0;
                else
                    MAP[i, j] = int.Parse(tileNums[i], hexNum);
                //после помещени€ плитки на карту - проверка, надо ли еЄ заменить
                CheckTileSwaps(i, j);
            }
        }
        print("Parsed " + SPRITES.Length + " sprites.");
        print("Map size: " + W + " wide by " + H + " high");

        ShowMap();
    }
    /// <summary>
    /// ѕреобразование списка в словарь
    /// </summary>
    void PrepareTileSwapDict()
    {
        tileSwapDict = new Dictionary<int, TileSwap>();
        foreach (TileSwap ts in tileSwaps)
            tileSwapDict.Add(ts.tileNum, ts);
    }
    /// <summary>
    /// ѕроверка необходимости замены плитки и замена
    /// </summary>
    /// <param name="i"></param>
    /// <param name="j"></param>
    void CheckTileSwaps(int i, int j)
    {
        int tNum = GET_MAP(i, j);
        if (!tileSwapDict.ContainsKey(tNum)) return; //плитку не в списке замен - выход
        TileSwap ts = tileSwapDict[tNum];
        if(ts.swapPrefab != null)
        {
            GameObject go = Instantiate(ts.swapPrefab);
            Enemy e = go.GetComponent<Enemy>();
            if (e != null)
                go.transform.SetParent(enemyAnchor);
            else
                go.transform.SetParent(itemAnchor);
            go.transform.position = new Vector3(i, j, 0);
            if (ts.guaranteedItemDrop != null)
                if (e != null)
                    e.guaranteedItemDrop = ts.guaranteedItemDrop;
        }
        //замена оригинальной плитки на стандартную / заданную
        if (ts.overrideTileNum == -1)
            SET_MAP(i, j, defaultTileNum);
        else
            SET_MAP(i, j, ts.overrideTileNum);
    }
    void ShowMap()
    {
        TILES = new Tile[W, H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++) 
                if(MAP[i,j] != 0)
                {
                    Tile ti = Instantiate<Tile>(tilePrefab); // создать объект, получить ссылку на скрипт
                    ti.transform.SetParent(TILE_ANCHOR);
                    ti.SetTile(i, j);
                    TILES[i, j] = ti;
                }
    }
    static public int GET_MAP(int x, int y)
    {
        if (x < 0 || x >= W || y < 0 || y >= H)
            return -1;
        return MAP[x, y];
    }
    static public int GET_MAP(float x, float y)
    {
        int tX = Mathf.RoundToInt(x);
        int tY = Mathf.RoundToInt(y - 0.25f); //сложна€ перспектива игры - иногда игрок должен находитьс€ ниже
        return GET_MAP(tX, tY);
    }
    static public void SET_MAP(int x, int y, int tNum)
    {
        if (x < 0 || x >= W || y < 0 || y >= H)
            return;
        MAP[x, y] = tNum;
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
