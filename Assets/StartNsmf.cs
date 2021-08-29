#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StartNsmf : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log(Nsmf.Smf.FromBytes(
          System.IO.File.ReadAllBytes("Assets/field.mid")
        ).header.division);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
