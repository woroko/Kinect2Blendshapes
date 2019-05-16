using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BlendshapeMapping : System.Object
{
    public string from = "";
    public float offset = 0.0f;
    public List<StringFloat> to;
}
