using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/**
界面上有三个输入框，分别对应 X,Y,Z 的值，请实现 {@link Q1.onGenerateBtnClick} 函数，生成一个 10 × 10 的可控随机矩阵，并显示到界面上，矩阵要求如下：
1. {@link COLORS} 中预定义了 5 种颜色
2. 每个点可选 5 种颜色中的 1 种
3. 按照从左到右，从上到下的顺序，依次为每个点生成颜色，(0, 0)为左上⻆点，(9, 9)为右下⻆点，(0, 9)为右上⻆点
4. 点(0, 0)随机在 5 种颜色中选取
5. 其他各点的颜色计算规则如下，设目标点坐标为(m, n）：
    a. (m, n - 1)所属颜色的概率为基准概率加 X%
    b. (m - 1, n)所属颜色的概率为基准概率加 Y%
    c. 如果(m, n - 1)和(m - 1, n)同色，则该颜色的概率为基准概率加 Z%
    d. 其他颜色平分剩下的概率
*/

public class Q1 : MonoBehaviour
{
    private static readonly Color[] COLORS = new Color[]
    {
        Color.red,
        Color.green,
        Color.blue,
        Color.yellow,
        new Color(1f, 0.5f, 0f) // Orange
    };

    // 每个格子的大小
    private const float GRID_ITEM_SIZE = 75f;

    [SerializeField]
    private InputField xInputField = null;

    [SerializeField]
    private InputField yInputField = null;

    [SerializeField]
    private InputField zInputField = null;

    [SerializeField]
    private Transform gridRootNode = null;

    [SerializeField]
    private GameObject gridItemPrefab = null;


    private const int GridSize = 10;

    public void OnGenerateBtnClick()
    {
        // TODO: 请在此处开始作答
        //转百分比
        float xPct = ParsePercent(xInputField);
        float yPct = ParsePercent(yInputField);
        float zPct = ParsePercent(zInputField);
        //判空
        if (gridRootNode == null || gridItemPrefab == null)
            return;

        EnsureGridLayout();
        //检查销毁
        for (int i = gridRootNode.childCount - 1; i >= 0; i--)
            Destroy(gridRootNode.GetChild(i).gameObject);

        int[,] colorIdx = new int[GridSize, GridSize];
        float[] probs = new float[COLORS.Length];

        for (int m = 0; m < GridSize; m++)
        {
            for (int n = 0; n < GridSize; n++)
            {
                int idx;
                if (m == 0 && n == 0)
                {
                    idx = Random.Range(0, COLORS.Length);
                }
                else
                {
                    FillNeighborProbabilities(colorIdx, m, n, xPct, yPct, zPct, probs);
                    idx = PickByWeights(probs);
                }

                colorIdx[m, n] = idx;
                InstantiateGridCell(idx);
            }
        }
    }


    // 从输入框解析百分比数值，空或非法则返回 0。
    private static float ParsePercent(InputField field)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.text))
            return 0f;

        float v;
        return float.TryParse(field.text.Trim(), out v) ? v : 0f;
    }



    // 为根节点配置 10 列网格式布局（左上起、先行后列）。
    private void EnsureGridLayout()
    {
        var gridLayout = gridRootNode.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
            gridLayout = gridRootNode.gameObject.AddComponent<GridLayoutGroup>();

        gridLayout.cellSize = new Vector2(GRID_ITEM_SIZE, GRID_ITEM_SIZE);
        gridLayout.spacing =new Vector2(2,2); //行列间距
        //排列方式
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = GridSize;
    }


    // 按左邻/上邻与 X、Y、Z 规则写出 (m,n) 处五种颜色的概率数组（基准各 20%）。
    private static void FillNeighborProbabilities(int[,] grid, int m, int n, float xPct, float yPct, float zPct, float[] probs)
    {
        const float basePct = 20f;
        int count = probs.Length;

        bool hasLeft = n > 0;
        bool hasTop = m > 0;

        if (!hasLeft && !hasTop)
        {
            for (int i = 0; i < count; i++)
                probs[i] = basePct;
            return;
        }

        if (hasLeft && !hasTop)
        {
            int leftIdx = grid[m, n - 1];
            DistributeSingleBoost(probs, basePct, leftIdx, xPct, count);
            return;
        }

        if (!hasLeft && hasTop)
        {
            int topIdx = grid[m - 1, n];
            DistributeSingleBoost(probs, basePct, topIdx, yPct, count);
            return;
        }

        int lc = grid[m, n - 1];
        int tc = grid[m - 1, n];

        if (lc == tc)
        {
            for (int i = 0; i < count; i++)
                probs[i] = 0f;

            probs[lc] = basePct + zPct;
            float rest = Mathf.Max(0f, 100f - probs[lc]);
            int others = count - 1;
            float each = others > 0 ? rest / others : rest;
            for (int i = 0; i < count; i++)
            {
                if (i != lc)
                    probs[i] = each;
            }
            return;
        }

        DistributeDistinctPair(probs, basePct, lc, tc, xPct, yPct, count);
    }

    // 仅一个参考邻格时：该色 基准+加成，其余颜色平分剩余概率。
    private static void DistributeSingleBoost(float[] probs, float basePct, int boostedIdx, float boostPct, int count)
    {
        for (int i = 0; i < count; i++)
            probs[i] = 0f;

        probs[boostedIdx] = basePct + boostPct;
        float rest = Mathf.Max(0f, 100f - probs[boostedIdx]);
        int others = count - 1;
        float each = others > 0 ? rest / others : rest;
        for (int i = 0; i < count; i++)
        {
            if (i != boostedIdx)
                probs[i] = each;
        }
    }

    // 左邻、上邻异色：两色分别加 X、Y，剩余三种颜色平分余下的概率。
    private static void DistributeDistinctPair(float[] probs, float basePct, int l, int t, float xPct, float yPct, int count)
    {
        for (int i = 0; i < count; i++)
            probs[i] = 0f;

        probs[l] = basePct + xPct;
        probs[t] = basePct + yPct;
        float rest = Mathf.Max(0f, 100f - probs[l] - probs[t]);
        int others = count - 2;
        float eachOther = others > 0 ? rest / others : rest;
        for (int i = 0; i < count; i++)
        {
            if (i != l && i != t)
                probs[i] = eachOther;
        }
    }

    // 按权重随机选颜色索引；总权重无效时使用均匀随机。
    private static int PickByWeights(float[] w)
    {
        float sum = 0f;
        for (int i = 0; i < w.Length; i++)
            sum += Mathf.Max(0f, w[i]);

        if (sum <= 1e-6f)
            return Random.Range(0, COLORS.Length);

        float r = Random.value * sum;
        for (int i = 0; i < w.Length; i++)
        {
            r -= Mathf.Max(0f, w[i]);
            if (r <= 0f)
                return i;
        }

        return w.Length - 1;
    }


    // 例化预制体并填色
    private void InstantiateGridCell(int colorIdx)
    {
        GameObject go = Instantiate(gridItemPrefab, gridRootNode);
        var image = go.GetComponent<Image>();
        if (image != null)
            image.color = COLORS[colorIdx];
    }
}
