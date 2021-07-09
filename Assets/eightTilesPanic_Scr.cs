using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

public class eightTilesPanic_Scr : MonoBehaviour {

    public KMAudio Audio;
    public KMBombInfo bomb;

    public KMSelectable[] tiles;
    public MeshRenderer[] mainItems; // aliens, guns, hamburger, none
    public MeshRenderer[] secondaryItems; // cats, people, none.

    public Material greenMaterial; // green material for solved tiles.

    public Material[] items; // 0=alien, 1=gun, 2=hamburger, 3=cat, 4=person, 5=none.

    private bool[] alienPresent = new bool[8] {false, false, false, false, false, false, false, false};
    private bool[] gunPresent = new bool[8] {false, false, false, false, false, false, false, false};
    private bool[] burgerPresent = new bool[8] {false, false, false, false, false, false, false, false};
    private bool[] catPresent = new bool[8] {false, false, false, false, false, false, false, false};
    private bool[] personPresent = new bool[8] {false, false, false, false, false, false, false, false};

    private int[] itemsOnStreet = new int[5]; // [0=alien, 1=gun, 2=hamburger, 3=cat, 4=person]
    private int[,] itemsOnSideStreet = new int[3, 5]; // [side street 0 to 2, item]
    
    private int[,] streetTiles = new int[10, 5] {
        {0, 1, 3, 4, 7}, {0, 1, 3, 2, 5}, {0, 2, 5, 6, 7}, {7, 6, 3, 2, 5}, {0, 2, 3, 6, 7}, {4, 3, 2, 5, 6}, {4, 3, 1, 0, 2}, {0, 1, 3, 6, 5}, {7, 6, 3, 1, 0}, {1, 0, 2, 3, 6}
    };

    private int[,,] sideStreets = new int[10, 3, 3] { // [main street, side streets 1-3, tiles of side street 1-3], -1 means not present
        { {2, 5, 6}, {-1, -1, -1}, {-1, -1, -1} }, { {4, 6, 7}, {-1, -1, -1}, {-1, -1, -1} }, //256, 467
        { {1, 3, 4}, {-1, -1, -1}, {-1, -1, -1} }, { {0, 1, -1}, {4, -1, -1}, {-1, -1, -1} }, //134, 01-4
        { {1, -1, -1}, {4, -1, -1}, {5, -1, -1} }, { {0, 1, -1}, {7, -1, -1}, {-1, -1, -1} }, //1-4-5, 01-7
        { {5, 6, 7}, {-1, -1, -1}, {-1, -1, -1} }, { {4, 7, -1}, {2, -1, -1}, {-1, -1, -1} }, //567, 47-2
        { {2, 5, -1}, {4, -1, -1}, {-1, -1, -1} }, { {4, 7, -1}, {5, -1, -1}, {-1, -1, -1} }  //25-4, 47-5
    };

    private int numberOfSideStreets = 0;
    private int streetToUse;
 
    private bool[] correctRules = new bool[12] {false, false, false, false, false, false, false, false, false, false, false, false};
    private int[] ruleAtTile = new int[5]; //these are the conditions (true/false) for the 5 tiles of the main street in order. (a fals means to tap the tile. A true means to hold the tile.)
    private int stage = 0;
    private int numberOfStages;

    private bool buttonHeld = false;
    private KMSelectable lastButtonPressed;
    private bool wrongButtonPressed = false;
    private float timePassed = 0f;
  
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved = false;

    private string[] interactionLog = new string[5];


    void Awake()
    {
        moduleId = moduleIdCounter++;
        //disable tiles until the lights turn on
        foreach (MeshRenderer item in mainItems)
        {
            item.gameObject.SetActive(false);
        }
        foreach (MeshRenderer item in secondaryItems)
        {
            item.gameObject.SetActive(false);
        }

        foreach (KMSelectable tile in tiles)
        {
            KMSelectable pressedTile = tile;
            tile.OnInteract += delegate () {TilePress(pressedTile); return false; };
            tile.OnInteractEnded += TileRelease;
        }
        
        GetComponent<KMBombModule>().OnActivate += OnActivate;
    }

    void OnActivate()
    {
        foreach (MeshRenderer item in mainItems)
        {
            item.gameObject.SetActive(true);
        }
        foreach (MeshRenderer item in secondaryItems)
        {
            item.gameObject.SetActive(true);
        }
    }

    // Use this for initialization
    void Start() 
    {
        PopulateGrid();
        FindStreetToUse();
        CountItemsOnStreets();
        CheckRules();
        AssignRules();
    }
    
    // Update is called once per frame
    //void Update () 

    void PopulateGrid()
    {
        //for each tile decide whether there is an alien, a laser gun, a hamburger or none of them present.
        for (int i = 0; i < 8; i++)
        {
            int rone = UnityEngine.Random.Range(0, 4);
            switch (rone) //0 = alien, 1 = gun, 2 = burger, 3 = none
            {
                case 0: // alien
                    alienPresent[i] = true;
                    mainItems[i].material = items[0];
                    break;

                case 1: // gun
                    gunPresent[i] = true;
                    mainItems[i].material = items[1];
                    break;
                
                case 2: // burger
                    burgerPresent[i] = true;
                    mainItems[i].material = items[2];
                    break;

                case 3: // none
                    mainItems[i].material = items[5];
                    break;
            }

            int rtwo = UnityEngine.Random.Range(0, 3);
            switch (rtwo) //0=cat, 1=person, 2=none;
            {
                case 0: // cat
                    catPresent[i] = true;
                    secondaryItems[i].material = items[3];
                    break;

                case 1: //person
                    personPresent[i] = true;
                    secondaryItems[i].material = items[4];
                    break;

                case 2: //none
                    secondaryItems[i].material = items[5];
                    break;
            }
        }
    }

    void FindStreetToUse()
    {
        streetToUse = (int) bomb.GetSerialNumberNumbers().Last();
        
        //Debug.LogFormat("[Eight Tiles Panic #{0}] Last digit of serial number is: {1}.", moduleId, streetToUse);
        Debug.LogFormat("[Eight Tiles Panic #{0}] Street {1} follows the tiles: {2}, {3}, {4}, {5}, {6}. (Tiles are numbered 0-7 in reading order)", moduleId, streetToUse, streetTiles[streetToUse, 0], streetTiles[streetToUse, 1], streetTiles[streetToUse, 2], streetTiles[streetToUse, 3], streetTiles[streetToUse, 4]);
    }

    void CountItemsOnStreets()
    {
        //Count items on the main street. (always 5 tiles long)
        for (int i = 0; i < 5; i++)
        {
            if (alienPresent[streetTiles[streetToUse, i]]) { itemsOnStreet[0] += 1; }
            if (gunPresent[streetTiles[streetToUse, i]]) { itemsOnStreet[1] += 1; }
            if (burgerPresent[streetTiles[streetToUse, i]]) { itemsOnStreet[2] += 1; }
            if (catPresent[streetTiles[streetToUse, i]]) { itemsOnStreet[3] += 1; }
            if (personPresent[streetTiles[streetToUse, i]]) { itemsOnStreet[4] += 1; }
        }
        Debug.LogFormat("[Eight Tiles Panic #{0}] The main street contains: {1} Aliens, {2} Laser Guns, {3} Hamburger, {4} Cats, {5} People.", moduleId, itemsOnStreet[0], itemsOnStreet[1], itemsOnStreet[2], itemsOnStreet[3], itemsOnStreet[4]);

        //Count the number of side streets.
        for (int i = 0; i < 3; i++)
        {
            if (sideStreets[streetToUse, i, 0] == -1)
            { break; }
            else
            {
                numberOfSideStreets += 1;
            }
        }
        //Debug.LogFormat("[Eight Tiles Panic #{0}] There are {1} side streets.", moduleId, numberOfSideStreets);

        //Count items on each side street.
        for (int i = 0; i < numberOfSideStreets; i++) //run i for the number of side streets (1-3)
        {
            for (int j = 0; j < 4 - numberOfSideStreets; j++) //if there is 1 side street run j 3 times, 2-2 and, 3-1
            {
                if (sideStreets[streetToUse, i, j] == -1) { break; }
                if (alienPresent[sideStreets[streetToUse, i, j]]) { itemsOnSideStreet[i, 0] += 1; }
                if (gunPresent[sideStreets[streetToUse, i, j]]) { itemsOnSideStreet[i, 1] += 1; }
                if (burgerPresent[sideStreets[streetToUse, i, j]]) { itemsOnSideStreet[i, 2] += 1; }
                if (catPresent[sideStreets[streetToUse, i, j]]) { itemsOnSideStreet[i, 3] += 1; }
                if (personPresent[sideStreets[streetToUse, i, j]]) { itemsOnSideStreet[i, 4] += 1; }
            }
            Debug.LogFormat("[Eight Tiles Panic #{0}] Side street {1} contains: {2} Aliens, {3} Laser Guns, {4} Hamburger, {5} Cats, {6} People.", moduleId, i+1, itemsOnSideStreet[i, 0], itemsOnSideStreet[i, 1], itemsOnSideStreet[i, 2], itemsOnSideStreet[i, 3], itemsOnSideStreet[i, 4]);
        }
    }

    void CheckRules()
    {
        //rule 0: ALIEN + CAT: press if there is no gun present on any side street.
        correctRules[0] = true;
        for (int i = 0; i < numberOfSideStreets; i++)
        {
            if (itemsOnSideStreet[i, 1] > 0) {correctRules[0] = false; break; }
        }
        //rule 1: ALIEN + GUN: street with alien and gun present.
        for (int i = 0; i < (1 + numberOfSideStreets); i++)
        {
            if (i == numberOfSideStreets)
            {
                if (itemsOnStreet[0] > 0 && itemsOnStreet[1] > 0) { correctRules[1] = true; break; }
            }
            else
            {
                if (itemsOnSideStreet[i, 0] > 0 && itemsOnSideStreet[i, 1] > 0) {correctRules[1] = true; break;}
            }
        }
        //rule 2: ALIEN + NONE: a street with at least 3 aliens.
        for (int i = 0; i < (1 + numberOfSideStreets); i++)
        {
            if (i == numberOfSideStreets)
            {
                if (itemsOnStreet[0] > 2) { correctRules[2] = true; break; }
            }
            else
            {
                if (itemsOnSideStreet[i, 0] > 2) {correctRules[2] = true; break;}
            }
        }
        //rule 3: GUN + CAT: press if less than 3 batteries are present on the bomb.
        if (bomb.GetBatteryCount() < 3) {correctRules[3] = true;}
        //rule 4: GUN + PERSON: press if there are 3 or more aliens on the module.
        if (CountItems(0) > 2) {correctRules[4] = true;}
        //rule 5: GUN + NONE: a street with alien but no cat.
        for (int i = 0; i < (1 + numberOfSideStreets); i++)
        {
            if (i == numberOfSideStreets)
            {
                if (itemsOnStreet[0] > 0 && itemsOnStreet[3] == 0) { correctRules[5] = true; break; }
            }
            else
            {
                if (itemsOnSideStreet[i, 0] > 0 && itemsOnSideStreet[i, 3] == 0) {correctRules[5] = true; break;}
            }
        }
        //rule 6: HAMBURGER + CAT: press if there are more burgers than aliens on the module:
        if (CountItems(2) > CountItems(0)) {correctRules[6] = true;}
        //rule 7: HAMBURGER + PERSON: press if there is a indicator BOB present:
        if (bomb.IsIndicatorPresent("BOB")) {correctRules[7] = true;}
        //rule 8: HAMBURGER + NONE: if there are 2 or more people present on the module:
        if (CountItems(4) > 1) {correctRules[8] = true;}
        //rule 9: NONE + CAT: if there are more cats than people on the module:
        if (CountItems(3) > CountItems(4)) {correctRules[9] = true;}
        //rule 10: NONE + PERSON: if there is a gun on the main street.
        if (itemsOnStreet[1] > 0) {correctRules[10] = true;}
        //rule 11: NONE AT ALL: press if module isn't solved ;P
        correctRules[11] = true;

        Debug.LogFormat("[Eight Tiles Panic #{0}] RULES:", moduleId);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (1) There is no gun present on a side street: {1}.", moduleId, correctRules[0]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (2) There is a street with both an alien and a gun present: {1}.", moduleId, correctRules[1]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (3) There is a street with at least 3 aliens present: {1}.", moduleId, correctRules[2]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (4) There are less than 3 batteries present on the bomb: {1}.", moduleId, correctRules[3]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (5) There are 3 or more aliens present on the module: {1}.", moduleId, correctRules[4]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (6) There is an alien but no cat: {1}.", moduleId, correctRules[5]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (7) There are more burgers than aliens present on the module: {1}.", moduleId, correctRules[6]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (8) There is an indicator labeled BOB present on the bomb: {1}.", moduleId, correctRules[7]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (9) There are 2 or more people present on the module: {1}.", moduleId, correctRules[8]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (10) There are more cats than people present on the module: {1}.", moduleId, correctRules[9]);
        Debug.LogFormat("[Eight Tiles Panic #{0}] (11) There is a gun present on main street used: {1}.", moduleId, correctRules[10]);
    }

    void AssignRules()
    {
        for (int i = 0; i < 5; i++)
        {
            if (alienPresent[streetTiles[streetToUse, i]])
            {
                if (catPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 0;
                }
                else if (personPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 1;
                }
                else 
                {
                    ruleAtTile[i] = 2;
                }
            }
            else if (gunPresent[streetTiles[streetToUse, i]])
            {
                if (catPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 3;
                }
                else if (personPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 4;
                }
                else 
                {
                    ruleAtTile[i] = 5;
                }
            }
            else if (burgerPresent[streetTiles[streetToUse, i]])
            {
                if (catPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 6;
                }
                else if (personPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 7;
                }
                else 
                {
                    ruleAtTile[i] = 8;
                }
            }
            else
            {
                if (catPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 9;
                }
                else if (personPresent[streetTiles[streetToUse, i]])
                {
                    ruleAtTile[i] = 10;
                }
                else 
                {
                    ruleAtTile[i] = 11;
                }
            }
        }
        Debug.LogFormat("[Eight Tiles Panic #{0}] The rules in use along the main street's tiles are: {1}-({2}) / {3}-({4}) / {5}-({6}) / {7}-({8}) / {9}-({10}).", moduleId, streetTiles[streetToUse, 0], ruleAtTile[0]+1, streetTiles[streetToUse, 1], ruleAtTile[1]+1, streetTiles[streetToUse, 2], ruleAtTile[2]+1, streetTiles[streetToUse, 3], ruleAtTile[3]+1, streetTiles[streetToUse, 4], ruleAtTile[4]+1);

        for (int i = 0; i < 5; i++)
        {
            if (correctRules[ruleAtTile[i]])
            {
                interactionLog[i] = "HOLD";
            }
            else
            {
                interactionLog[i] = "TAP";
            }
        }
        /*Debug.LogFormat("[Eight Tiles Panic #{0}] The truth values along the main street's tiles are: {1}, {2}, {3}, {4}, {5}.", moduleId, correctRules[ruleAtTile[0]], correctRules[ruleAtTile[1]], correctRules[ruleAtTile[2]], correctRules[ruleAtTile[3]], correctRules[ruleAtTile[4]]);*/
        Debug.LogFormat("[Eight Tiles Panic #{0}] The interactions along the main street's tiles are: {1}, {2}, {3}, {4}, {5}.", moduleId, interactionLog[0], interactionLog[1], interactionLog[2], interactionLog[3], interactionLog[4]);
    }

    void TilePress(KMSelectable tile)
    {
        if (moduleSolved) {return;}
        tile.AddInteractionPunch();
        wrongButtonPressed = false;
        buttonHeld = true;
        lastButtonPressed = tile;
        StartCoroutine(ButtonAnimation(tile));

        if (Convert.ToInt32(tile.name) != streetTiles[streetToUse, stage]) //if you interacted with a tile out of order.
        {
            wrongButtonPressed = true;
            Debug.LogFormat("[Eight Tiles Panic #{0}] You pressed tile: {1}. Module expected: {2}. Strike!", moduleId, tile.name, streetTiles[streetToUse, stage]);
            GetComponent<KMBombModule>().HandleStrike();	
        }
        else
        {
            StartCoroutine(TimeWhileHolding());
            StartCoroutine(PlayLaserSound());
            Debug.LogFormat("[Eight Tiles Panic #{0}] You pressed tile: {1}.", moduleId, tile.name);
        }
    }

    void TileRelease()
    {
        if (moduleSolved) {return;}
        buttonHeld = false;
        StartCoroutine(ButtonAnimation(lastButtonPressed));
        if (wrongButtonPressed) {return;}
        if (timePassed >= 0.6f)
        {
            Debug.LogFormat("[Eight Tiles Panic #{0}] You released after {1} seconds: That was a HOLD.", moduleId, timePassed);
            if (correctRules[ruleAtTile[stage]]) //if the rule at the tile = to the stage is TRUE
            {
                lastButtonPressed.GetComponent<MeshRenderer>().material = greenMaterial;
                Audio.PlaySoundAtTransform("seq" + stage, transform);
                if (stage < 4)
                {
                    stage++;
                    Debug.LogFormat("[Eight Tiles Panic #{0}] Correct. {1} more to go. Next up is tile {2}.", moduleId, 5-stage, streetTiles[streetToUse, stage]);
                }
                else
                {
                    Debug.LogFormat("[Eight Tiles Panic #{0}] Correct. Module solved!", moduleId);
                    GetComponent<KMBombModule>().HandlePass();
                    moduleSolved = true;
                }
            }
            else
            {
                Debug.LogFormat("[Eight Tiles Panic #{0}] Module expected a TAP. Strike!", moduleId);
                GetComponent<KMBombModule>().HandleStrike();
            }
        }

        else 
        {
            Debug.LogFormat("[Eight Tiles Panic #{0}] You released after {1} seconds: That was a TAP.", moduleId, timePassed);
            if (correctRules[ruleAtTile[stage]]) //if the rule at the tile = stage is TRUE
            {
                Debug.LogFormat("[Eight Tiles Panic #{0}] Module expected a HOLD. Strike!", moduleId);
                GetComponent<KMBombModule>().HandleStrike();
            }
            else
            {
                lastButtonPressed.GetComponent<MeshRenderer>().material = greenMaterial;
                Audio.PlaySoundAtTransform("seq" + stage, transform);
                if (stage < 4)
                {
                    stage++;
                    Debug.LogFormat("[Eight Tiles Panic #{0}] Correct. {1} more to go. Next up is tile {2}.", moduleId, 5-stage, streetTiles[streetToUse, stage]);
                }
                else
                {
                    Debug.LogFormat("[Eight Tiles Panic #{0}] Correct. Module solved!", moduleId);
                    GetComponent<KMBombModule>().HandlePass();
                    moduleSolved = true;
                }	
            }
        }
        timePassed = 0f;
    }


    int CountItems(int itemType)
    {
        int count = 0;
        switch (itemType)
        {
            case 0: //aliens
                for (int i = 0; i < 8; i++)
                {
                    if (alienPresent[i]) {count += 1;}
                }
                break;
            
            case 1: //guns
                for (int i = 0; i < 8; i++)
                {
                    if (gunPresent[i]) {count += 1;}
                }
                break;

            case 2: //hamburger
                for (int i = 0; i < 8; i++)
                {
                    if (burgerPresent[i]) {count += 1;}
                }
                break;
            
            case 3: //cats
                for (int i = 0; i < 8; i++)
                {
                    if (catPresent[i]) {count += 1;}
                }
                break;
            
            case 4: //people
                for (int i = 0; i < 8; i++)
                {
                    if (personPresent[i]) {count += 1;}
                }
                break;
        }
        return count;
    }
    
    int Mod(int x, int m) // modulo function that always gives a positive value back
    {
        return (x % m + m) % m;
    }

    IEnumerator TimeWhileHolding()
    {
        while (buttonHeld)
        {
            yield return null;
            timePassed += Time.deltaTime;
        }
    }

    IEnumerator PlayLaserSound()
    {
        yield return new WaitForSeconds(0.6f);
        if(buttonHeld && timePassed >= 0.6f)
        {
            Audio.PlaySoundAtTransform("laser", transform);
        }
    }

    IEnumerator ButtonAnimation(KMSelectable pressedButton)
    {
        int movement = 0;
        if (buttonHeld)
        {
            while (movement < 5)
            {
                yield return new WaitForSeconds(0.0001f);
                pressedButton.transform.localPosition += Vector3.up * -0.001f;
                movement++;
            }
        }
        else
        {
            movement = 0;
            while (movement < 5)
            {
                yield return new WaitForSeconds(0.0001f);
                pressedButton.transform.localPosition += Vector3.up * 0.001f;
            movement++;
            }
        }
        StopCoroutine("buttonAnimation");
    }
}
