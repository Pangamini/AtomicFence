using System;
using System.Text;
using System.Threading.Tasks;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using TMPro;
using UnityEngine;
public class GameRoot : MonoBehaviour
{
    [SerializeField]
    private TMP_Text m_output;
    
    private FirebaseApp m_firebaseApp;
    private bool m_firebaseReady;

    async void Awake()
    {
        m_firebaseReady = false;
        await EnableFirebase();
        
        if(!m_firebaseReady)
            return;

        var firestore = FirebaseFirestore.DefaultInstance;
        firestore.Settings.PersistenceEnabled = false;
        Debug.Log(firestore);
        QuerySnapshot snapshot = await firestore.Collection("Fence").GetSnapshotAsync();

        StringBuilder output = new();
        foreach (DocumentSnapshot snapshotDocument in snapshot.Documents)
        {
            CellDTO cell = snapshotDocument.ConvertTo<CellDTO>();
            output.AppendLine(cell.ToString());
        }
        m_output.text = output.ToString();
        // Debug.Log(snapshot);
    }

    private void OnDestroy()
    {
        m_firebaseApp.Dispose();
    }

    async Task EnableFirebase()
    {
        await FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            var dependencyStatus = task.Result;
            if(dependencyStatus == DependencyStatus.Available)
            {
                // Create and hold a reference to your FirebaseApp,
                // where app is a Firebase.FirebaseApp property of your application class.
                m_firebaseApp = FirebaseApp.DefaultInstance;

                // Set a flag here to indicate whether Firebase is ready to use by your app.
                m_firebaseReady = true;
                Debug.Log("Firebase initialized");
            }
            else
            {
                Debug.LogError(String.Format("Could not resolve all Firebase dependencies: {0}", dependencyStatus));
                // Firebase Unity SDK is not safe to use here.
            }
        });
    }
}