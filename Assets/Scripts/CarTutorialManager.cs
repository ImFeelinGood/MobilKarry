using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarTutorialManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Ezereal.EzerealCarController car;
    [SerializeField] private CanvasGroup tutorialPanel;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Button continueButton;

    [Header("Tutorial Data")]
    [SerializeField] private List<CarTutorialStep> gearboxOnSteps = new();
    [SerializeField] private List<CarTutorialStep> gearboxOffSteps = new();

    [Header("Settings")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool saveCompletion = true;
    [SerializeField] private string playerPrefsKey = "car_tutorial_done";

    private List<CarTutorialStep> activeSteps;
    private int currentStepIndex;
    private bool waitingForContinue;

    private void Start()
    {
        if (saveCompletion && PlayerPrefs.GetInt(playerPrefsKey, 0) == 1)
        {
            HideTutorial();
            return;
        }

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinuePressed);

        if (playOnStart)
            StartTutorial();
    }

    public void StartTutorial()
    {
        activeSteps = car.useGearbox ? gearboxOnSteps : gearboxOffSteps;
        currentStepIndex = 0;
        ShowTutorial();
        StartCoroutine(RunTutorial());
    }

    private IEnumerator RunTutorial()
    {
        while (currentStepIndex < activeSteps.Count)
        {
            var step = activeSteps[currentStepIndex];
            instructionText.text = step.instruction;

            yield return new WaitUntil(() => IsStepComplete(step));

            currentStepIndex++;
        }

        instructionText.text = "Tutorial complete.";
        yield return new WaitForSeconds(1f);

        if (saveCompletion)
        {
            PlayerPrefs.SetInt(playerPrefsKey, 1);
            PlayerPrefs.Save();
        }

        HideTutorial();
    }

    private bool IsStepComplete(CarTutorialStep step)
    {
        switch (step.stepType)
        {
            case CarTutorialStepType.StartCar:
                return car.IsStarted;

            case CarTutorialStepType.ToggleGearboxOn:
                return car.useGearbox;

            case CarTutorialStepType.ToggleGearboxOff:
                return !car.useGearbox;

            case CarTutorialStepType.ShiftToDrive:
                return car.useGearbox && car.CurrentGear == Ezereal.AutomaticGears.Drive;

            case CarTutorialStepType.ShiftToReverse:
                return car.useGearbox && car.CurrentGear == Ezereal.AutomaticGears.Reverse;

            case CarTutorialStepType.MoveForward:
                return car.CurrentSpeed > 5f;

            case CarTutorialStepType.BrakeToStop:
                return Mathf.Abs(car.CurrentSpeed) < 1f && car.BrakeInput > 0f;

            case CarTutorialStepType.Reverse:
                return car.CurrentSpeed < -3f;

            case CarTutorialStepType.Steer:
                return Mathf.Abs(car.SteerInput) > 0.1f;

            case CarTutorialStepType.Handbrake:
                return car.HandbrakeInput > 0.1f;

            case CarTutorialStepType.Complete:
                return waitingForContinue;

            default:
                return false;
        }
    }

    private void OnContinuePressed()
    {
        waitingForContinue = true;
    }

    private void ShowTutorial()
    {
        if (tutorialPanel == null) return;

        tutorialPanel.alpha = 1f;
        tutorialPanel.interactable = true;
        tutorialPanel.blocksRaycasts = true;
    }

    private void HideTutorial()
    {
        if (tutorialPanel == null) return;

        tutorialPanel.alpha = 0f;
        tutorialPanel.interactable = false;
        tutorialPanel.blocksRaycasts = false;
    }
}