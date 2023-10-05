using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.Events;
public class FirebaseInitializer : MonoBehaviour
{
    [SerializeField]
    private UnityEvent m_firebaseReady = new();
    private FirebaseApp m_firebaseApp;

    async void Awake()
    {
        bool firebaseReady = await EnableFirebase();
        
        if(!firebaseReady)
            return;

        #if UNITY_EDITOR
        FirebaseFirestore.DefaultInstance.Settings.PersistenceEnabled = false; // persistence makes unity editor crash
        #endif

        
        m_firebaseReady.Invoke();
    }

    private void OnDestroy()
    {
        m_firebaseApp.Dispose();
    }

    async Task<bool> EnableFirebase()
    {
        return await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if(dependencyStatus == DependencyStatus.Available)
            {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                m_firebaseApp = FirebaseApp.DefaultInstance;

                // Set a flag here to indicate whether Firebase is ready to use by your app.
                Debug.Log("Firebase initialized");
                return true;
            }
            else
            {
                m_firebaseApp = null;
                Debug.LogError(String.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                // Firebase Unity SDK is not safe to use here.
                return false;
            }
        });
    }
}