using System;
using UnityEngine;

[Serializable]
public class CarTutorialStep
{
    [TextArea] public string instruction;
    public CarTutorialStepType stepType;
}