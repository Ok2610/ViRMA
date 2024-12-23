﻿using UnityEngine;

public class ViRMA_Colors : MonoBehaviour
{
    public static Color32 axisRed = new Color32(192, 57, 43, 255);
    public static Color32 axisGreen = new Color32(39, 174, 96, 255);
    public static Color32 axisBlue = new Color32(35, 99, 142, 255);

    public static Color32 axisFadeRed = new Color32(192, 57, 43, 175);
    public static Color32 axisFadeGreen = new Color32(39, 174, 96, 175);
    public static Color32 axisFadeBlue = new Color32(35, 99, 142, 175);

    public static Color32 axisDarkRed = new Color32(166, 33, 18, 255);
    public static Color32 axisDarkGreen = new Color32(22, 145, 73, 255);
    public static Color32 axisDarkBlue = new Color32(21, 82, 122, 255);

    public static Color32 darkBlue = new Color32(52, 73, 94, 255);
    public static Color32 lightBlue = new Color32(52, 152, 219, 255);
    public static Color32 darkGrey = new Color32(50, 50, 50, 255);
    public static Color32 grey = new Color32(99, 110, 114, 255);
    public static Color32 lightGrey = new Color32(200, 200, 200, 255);
    public static Color32 flatOrange = new Color32(230, 126, 34, 255);
    public static Color32 BrightenColor(Color32 colorToBrighten)
    {
        float H, S, V;
        Color.RGBToHSV(colorToBrighten, out H, out S, out V);
        Color32 brighterColor = Color.HSVToRGB(H, S * 0.80f, V / 0.60f);
        return brighterColor;
    }
    public static Color32 DarkenColor(Color32 colorToDarken)
    {
        float H, S, V;
        Color.RGBToHSV(colorToDarken, out H, out S, out V);
        Color32 darkerColor = Color.HSVToRGB(H, S / 0.80f, V * 0.60f);
        return darkerColor;
    }
}
