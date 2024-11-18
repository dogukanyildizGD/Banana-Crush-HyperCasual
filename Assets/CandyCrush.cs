using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class CandyCrush : MonoBehaviour
{
    public List<GameObject> boxPrefabs;
    public GameObject obstaclePrefab;
    public int width;
    public int height;
    public Vector2 boxSpacing = new Vector2(0.1f, 0.1f);
    public float boxSwapDuration = 0.01f;
    public float boxDiagonalDuration = 0.6f;
    public int minMatchesToExplode = 3;

    [SerializeField] private float explosionEffectDuration = 0.2f;
    [SerializeField] private float explosionScale = 0.5f;

    public GameObject[,] allBoxes;
    private GameObject[,] allObstacles;
    private GameObject selectedBox;

    public bool isGameBusy = false;
    public bool hasBoxMoved = false;

    void Start()
    {
        // Get the selected level from PlayerPrefs
        string levelFile = PlayerPrefs.GetString("selectedLevel", "level1"); // Default is "level1" if nothing is set

        StartCoroutine(LoadAndSetupGame(levelFile));
        CameraSettings();
    }

    public IEnumerator LoadAndSetupGame(string levelFileName)
    {
        TextAsset levelFile = Resources.Load<TextAsset>(levelFileName);
        if (levelFile != null)
        {
            string levelData = levelFile.text;
            CalculateBoardSize(levelData);
            allBoxes = new GameObject[width, height];
            yield return StartCoroutine(SetupBoard(levelData));
            yield return StartCoroutine(CheckMatchesAndRefill());
        }
        else
        {
            Debug.Log("Failed to load file: " + levelFileName);
        }
    }

    public IEnumerator SetupBoard(string levelData)
    {
        isGameBusy = true;

        string[] lines = levelData.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        height = lines.Length;
        width = lines[0].Length;

        allBoxes = new GameObject[width, height];
        allObstacles = new GameObject[width, height];

        for (int j = height - 1; j >= 0; j--)
        {
            string line = lines[height - 1 - j];
            for (int i = 0; i < width; i++)
            {
                if (i < line.Length)
                {
                    char symbol = line[i];
                    Vector2 position = new Vector2(i + (boxSpacing.x * i), j + (boxSpacing.y * j));

                    if (symbol == '#')
                    {
                        // Engel prefabını bu pozisyonda oluşturun
                        GameObject obstacle = Instantiate(obstaclePrefab, position, Quaternion.identity);
                        obstacle.transform.parent = transform;
                        obstacle.name = "Obstacle_" + i + "_" + j;
                        allObstacles[i, j] = obstacle;
                    }
                    else if (symbol == '.' && allObstacles[i, j] == null)
                    {
                        // Normal kutu prefabını bu pozisyonda oluşturun
                        GameObject boxToCreate = GetRandomBoxPrefab();
                        GameObject newBox = Instantiate(boxToCreate, position, Quaternion.identity);
                        newBox.transform.parent = transform;
                        newBox.name = "Box_" + i + "_" + j;
                        allBoxes[i, j] = newBox;

                        // Yeni kutuların z-koordinatlarını düzenle
                        Vector3 newPosition = newBox.transform.position;
                        newPosition.z = -j; // Yükseklik sırasına göre z-koordinatını ayarla
                        newBox.transform.position = newPosition;
                    }
                }
            }
        }

        isGameBusy = false;
        yield return null;
    }

    public IEnumerator SetupAndStartGame(string fileContent)
    {
        yield return StartCoroutine(SetupBoard(fileContent));
        yield return StartCoroutine(CheckMatchesAndRefill());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePosition, Vector2.zero);

            if (hit.collider != null)
            {
                GameObject clickedBox = hit.collider.gameObject;
                HandleMouseClick(clickedBox);
            }
        }
    }

    private void CalculateBoardSize(string fileContent)
    {
        string[] lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        height = lines.Length;
        width = lines[0].Length;
    }

    public void CameraSettings()
    {
        float spriteWidth = boxPrefabs[0].GetComponent<SpriteRenderer>().bounds.size.x;
        float spriteHeight = boxPrefabs[0].GetComponent<SpriteRenderer>().bounds.size.y;

        // Sağ ve sol taraftaki boşluk miktarı
        float horizontalPadding = spriteWidth;
        float verticalPadding = spriteHeight;

        // Tahta boyutunu hesapla
        float boardWidth = width + (boxSpacing.x * (width - 1));
        float boardHeight = height + (boxSpacing.y * (height - 1));

        // Kamera boyutunu ve pozisyonunu ayarla
        Camera camera = Camera.main;
        camera.transform.position = new Vector3((width - 1) / 2f + boxSpacing.x * (width - 1) / 2f, (height - 1) / 2f + boxSpacing.y * (height - 1) / 2f, camera.transform.position.z);

        // Ekran boyutunu al
        float screenHeight = 2f * camera.orthographicSize;
        float screenWidth = screenHeight * camera.aspect;

        // Tahta boyutuna göre kamera boyutunu ayarla
        float targetCameraSize = Mathf.Max(boardWidth + (2 * horizontalPadding), boardHeight);

        if (screenWidth < boardWidth || screenHeight < boardHeight)
        {
            camera.orthographicSize = targetCameraSize;
        }
        else
        {
            float targetScreenRatio = screenWidth / screenHeight;
            float boardRatio = boardWidth / boardHeight;

            if (targetScreenRatio >= boardRatio)
            {
                float targetCameraSizeWithSpacing = targetCameraSize * (0.75f + boxSpacing.x / 2f);
                camera.orthographicSize = targetCameraSizeWithSpacing * targetScreenRatio / boardRatio;
            }
            else
            {
                float targetCameraSizeWithSpacing = targetCameraSize * (0.75f + boxSpacing.y / 2f);
                camera.orthographicSize = targetCameraSizeWithSpacing * targetScreenRatio / boardRatio;
            }
        }
    }

    public void HandleMouseClick(GameObject clickedBox)
    {
        if (isGameBusy) return;

        if (selectedBox == null)
        {
            selectedBox = clickedBox;
            // Seçili kutuyu işaretlemek için burada gerekli işlemleri yapabilirsiniz
        }
        else if (IsAdjacent(selectedBox, clickedBox))
        {
            // Farklı bir kutuya tıklandı, kutuları takas et
            StartCoroutine(TrySwapBoxes(selectedBox, clickedBox));
            // selectedBox'u sıfırla
            selectedBox = null;
            // Seçili kutunun işaretini kaldırmak için burada gerekli işlemleri yapabilirsiniz
        }
        else
        {
            Debug.Log("Kutular çapraz şekilde seçilemez veya birbirine bitişik değildir.");
            selectedBox = clickedBox; // Seçili kutuyu yeni kutuya güncelle
                                      // Seçili kutuyu işaretlemek için burada gerekli işlemleri yapabilirsiniz
        }
    }

    public void SelectBox(GameObject box)
    {
        if (selectedBox == null)
        {
            selectedBox = box;
            // Seçili kutuyu işaretlemek için burada gerekli işlemleri yapabilirsiniz
        }
        else
        {
            if (IsAdjacent(selectedBox, box))
            {
                StartCoroutine(TrySwapBoxes(selectedBox, box));
            }
            selectedBox = null;
            // Seçili kutunun işaretini kaldırmak için burada gerekli işlemleri yapabilirsiniz
        }
    }

    private IEnumerator CheckMatchesAndRefill()
    {
        isGameBusy = true;

        // Daha sonra eşleşmeler kontrol edilir.
        var matchedGroup = FindFirstMatchGroup();
        if (matchedGroup != null)
        {
            yield return StartCoroutine(DestroyMatches(matchedGroup));

            yield return StartCoroutine(CollapseColumns());
            yield return StartCoroutine(RefillBoard());
        }

        Debug.Log("Has Possible Match: " + HasPossibleMatch());

        if (!HasPossibleMatch())
        {
            // Animasyonu oynat
            yield return StartCoroutine(ShowShuffleAnimation());
            yield return new WaitForSeconds(5f);
            yield return StartCoroutine(ShuffleBoard());
        }

        isGameBusy = false;
        yield return null;
    }

    private List<GameObject> FindFirstMatchGroup()
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                GameObject boxToCheck = allBoxes[i, j];
                if (boxToCheck == null) continue;

                List<GameObject> horizontalMatches = new List<GameObject> { boxToCheck };
                List<GameObject> verticalMatches = new List<GameObject> { boxToCheck };
                for (int x = i + 1; x < width; x++)
                {
                    GameObject boxToCompare = allBoxes[x, j];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        horizontalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }

                for (int y = j + 1; y < height; y++)
                {
                    GameObject boxToCompare = allBoxes[i, y];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        verticalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }

                if (horizontalMatches.Count >= minMatchesToExplode)
                {
                    return horizontalMatches;
                }
                if (verticalMatches.Count >= minMatchesToExplode)
                {
                    return verticalMatches;
                }
            }
        }

        return null;
    }

    private bool CheckMatchesAtPosition(int x, int y)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
        {
            GameObject boxToCheck = allBoxes[x, y];
            if (boxToCheck != null)
            {
                List<GameObject> horizontalMatches = new List<GameObject> { boxToCheck };
                List<GameObject> verticalMatches = new List<GameObject> { boxToCheck };

                // Check right
                for (int i = x + 1; i < width; i++)
                {
                    GameObject boxToCompare = allBoxes[i, y];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        horizontalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }

                // Check left
                for (int i = x - 1; i >= 0; i--)
                {
                    GameObject boxToCompare = allBoxes[i, y];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        horizontalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }            // Check up
                for (int j = y + 1; j < height; j++)
                {
                    GameObject boxToCompare = allBoxes[x, j];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        verticalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }

                // Check down
                for (int j = y - 1; j >= 0; j--)
                {
                    GameObject boxToCompare = allBoxes[x, j];
                    if (boxToCompare != null && boxToCheck.GetComponent<SpriteRenderer>().sprite == boxToCompare.GetComponent<SpriteRenderer>().sprite)
                    {
                        verticalMatches.Add(boxToCompare);
                    }
                    else
                    {
                        break;
                    }
                }

                if (horizontalMatches.Count >= minMatchesToExplode || verticalMatches.Count >= minMatchesToExplode)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator DestroyMatches(List<GameObject> matchGroup)
    {
        isGameBusy = true;

        List<Vector2Int> indices = new List<Vector2Int>();
        foreach (var box in matchGroup)
        {
            indices.Add(GetArrayIndexOfBox(box));
            StartCoroutine(ApplyExplosionEffect(box));
        }
        yield return new WaitForSeconds(explosionEffectDuration);

        foreach (var index in indices)
        {
            GameObject boxToDestroy = allBoxes[index.x, index.y];
            Destroy(boxToDestroy);
            allBoxes[index.x, index.y] = null;
        }

        yield return new WaitForSeconds(explosionEffectDuration);

        // Patlatılan kutuları dikkate alarak collapse işlemini gerçekleştir
        yield return StartCoroutine(CollapseColumns());
        yield return StartCoroutine(RefillBoard());

        // Eşleşmeler ve yeniden doldurma işlemi tamamlandıktan sonra tekrar kontrol et
        yield return StartCoroutine(CheckMatchesAndRefill());

        isGameBusy = false;
        yield return null;
    }

    private IEnumerator ApplyExplosionEffect(GameObject box)
    {
        isGameBusy = true;

        if (box == null) yield break;

        Vector3 originalScale = box.transform.localScale;
        Vector3 targetScale = originalScale * explosionScale;

        float elapsedTime = 0f;

        while (elapsedTime < explosionEffectDuration)
        {
            // box'ın hala mevcut olduğunu kontrol edin
            if (box == null) yield break;

            float t = elapsedTime / explosionEffectDuration;
            box.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (box != null)
        {
            box.transform.localScale = Vector3.zero;
        }

        isGameBusy = false;
        yield return null;
    }

    private IEnumerator ShowShuffleAnimation()
    {
        isGameBusy = true;

        // Animasyonu başlat
        GameObject animationObjectShuffle = GameObject.Find("shuffleOB");
        GameObject animationObjectMistBG = GameObject.Find("mistBG");

        if (animationObjectShuffle == null)
        {
            Debug.LogError("Animation object not found");
            yield break;
        }

        Animator mAnimator = animationObjectShuffle.GetComponent<Animator>();
        Animator m2Animator = animationObjectMistBG.GetComponent<Animator>();

        if (mAnimator != null)
        {
            mAnimator.SetTrigger("startShuffle");
        }

        if (m2Animator != null)
        {
            m2Animator.SetTrigger("mistT");
        }

        yield return null;

        isGameBusy = false;
    }

    private Vector3 GetWorldPositionFromGridPosition(int column, int row)
    {
        float xPosition = column * (1 + boxSpacing.x);
        float yPosition = row * (1 + boxSpacing.y);
        return new Vector3(xPosition, yPosition, 0);
    }

    private IEnumerator MoveBoxDownIfSpaceBelow()
    {
        bool isMoved = false;
        List<IEnumerator> moveCoroutines = new List<IEnumerator>();

        for (int column = 0; column < width; column++)
        {
            for (int row = 1; row < height; row++) // en alttaki sırayı dikkate almıyoruz çünkü altında başka bir kutu olamaz
            {
                // Eğer kutu varsa ve kutunun altında bir boşluk varsa:
                if (allBoxes[column, row] != null && allBoxes[column, row - 1] == null && allObstacles[column, row - 1] == null)
                {
                    // Hedef pozisyonu ve geçiş süresini belirleyin
                    Vector3 startPosition = allBoxes[column, row].transform.position;
                    Vector3 targetPosition = GetWorldPositionFromGridPosition(column, row - 1);

                    // Kutuyu aşağıya taşıyın
                    moveCoroutines.Add(MoveBoxes(allBoxes[column, row], startPosition, targetPosition, boxSwapDuration));

                    // Kutuları güncelleyin
                    allBoxes[column, row - 1] = allBoxes[column, row];
                    allBoxes[column, row] = null;

                    isMoved = true;
                }
            }
        }

        if (isMoved)
        {
            yield return StartCoroutine(StartCoroutinesInSameFrame(moveCoroutines));
        }

        yield return null;
    }

    private IEnumerator CollapseColumns()
    {
        isGameBusy = true;
        List<IEnumerator> moveCoroutines = new List<IEnumerator>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (allBoxes[x, y] == null && allObstacles[x, y] == null && !IsObstacleAbove(x, y))
                {
                    int boxAboveIndex = y + 1;
                    while (boxAboveIndex < height && allBoxes[x, boxAboveIndex] == null)
                    {
                        boxAboveIndex++;
                    }

                    if (boxAboveIndex < height)
                    {
                        GameObject box = allBoxes[x, boxAboveIndex];
                        Vector2 startPosition = box.transform.position;
                        Vector2 endPosition = new Vector2(x + (boxSpacing.x * x), y + (boxSpacing.y * y));

                        allBoxes[x, y] = box;
                        allBoxes[x, boxAboveIndex] = null;

                        moveCoroutines.Add(MoveBoxes(box, startPosition, endPosition, boxSwapDuration));
                    }
                }
            }
        }

        yield return StartCoroutine(StartCoroutinesInSameFrame(moveCoroutines));

        yield return StartCoroutine(MoveBoxDownIfSpaceBelow());

        isGameBusy = false;
    }

    private IEnumerator RefillBoard()
    {
        isGameBusy = true;

        List<IEnumerator> moveCoroutines = new List<IEnumerator>();
        bool moved;

        do
        {
            moved = false;
            moveCoroutines.Clear();

            for (int i = 0; i < width; i++)
            {
                bool isObstacleInColumn = false;

                if (allObstacles[i, height - 1] != null)
                {
                    isObstacleInColumn = true;
                }

                if (allBoxes[i, height - 1] == null && allObstacles[i, height - 1] == null && !isObstacleInColumn)
                {
                    Vector2 startPosition = new Vector2(i + (boxSpacing.x * i), height + (boxSpacing.y * height));
                    Vector2 endPosition = new Vector2(i + (boxSpacing.x * i), height - 1 + (boxSpacing.y * (height - 1)));
                    GameObject boxToCreate = GetRandomBoxPrefab();
                    GameObject newBox = Instantiate(boxToCreate, startPosition, Quaternion.identity);
                    newBox.transform.parent = transform;
                    newBox.name = "Box_" + i + "_" + (height - 1);
                    allBoxes[i, height - 1] = newBox;

                    moveCoroutines.Add(MoveBoxes(newBox, startPosition, endPosition, boxSwapDuration));
                    moved = true;
                }
            }

            yield return StartCoroutine(StartCoroutinesInSameFrame(moveCoroutines));
            moveCoroutines.Clear();

            for (int i = 0; i < width; i++)
            {
                for (int j = height - 2; j >= 0; j--)
                {
                    if (allBoxes[i, j] == null && allObstacles[i, j] == null && allBoxes[i, j + 1] != null)
                    {
                        Vector2 startPos = new Vector2(i + (boxSpacing.x * i), j + 1 + (boxSpacing.y * (j + 1)));
                        Vector2 endPos = new Vector2(i + (boxSpacing.x * i), j + (boxSpacing.y * j));
                        GameObject boxToMove = allBoxes[i, j + 1];
                        allBoxes[i, j + 1] = null;
                        allBoxes[i, j] = boxToMove;
                        moveCoroutines.Add(MoveBoxes(boxToMove, startPos, endPos, boxSwapDuration));
                        moved = true;
                    }
                }
            }

            yield return StartCoroutine(StartCoroutinesInSameFrame(moveCoroutines));
            moveCoroutines.Clear();

            for (int i = 0; i < width; i++)
            {
                for (int j = height - 1; j >= 0; j--)
                {
                    if (allObstacles[i, j] != null && j > 0 && allBoxes[i, j - 1] == null)
                    {
                        if (i > 0 && allBoxes[i - 1, j] != null)
                        {
                            Vector2 startPos = new Vector2(i - 1 + (boxSpacing.x * (i - 1)), j + (boxSpacing.y * j));
                            Vector2 endPos = new Vector2(i + (boxSpacing.x * i), j - 1 + (boxSpacing.y * (j - 1)));
                            GameObject boxToMove = allBoxes[i - 1, j];
                            allBoxes[i - 1, j] = null;
                            allBoxes[i, j - 1] = boxToMove;
                            moveCoroutines.Add(MoveBoxes(boxToMove, startPos, endPos, boxSwapDuration));
                            moved = true;
                        }
                        else if (i < width - 1 && allBoxes[i + 1, j] != null)
                        {
                            Vector2 startPos = new Vector2(i + 1 + (boxSpacing.x * (i + 1)), j + (boxSpacing.y * j));
                            Vector2 endPos = new Vector2(i + (boxSpacing.x * i), j - 1 + (boxSpacing.y * (j - 1)));
                            GameObject boxToMove = allBoxes[i + 1, j];
                            allBoxes[i + 1, j] = null;
                            allBoxes[i, j - 1] = boxToMove;
                            moveCoroutines.Add(MoveBoxes(boxToMove, startPos, endPos, boxSwapDuration));
                            moved = true;
                        }
                    }
                }
            }

            if (moved)
            {
                yield return StartCoroutine(StartCoroutinesInSameFrame(moveCoroutines));
            }

        } while (moved);

        yield return StartCoroutine(MoveBoxDownIfSpaceBelow());

        if (moved)
        {
            yield return StartCoroutine(CheckMatchesAndRefill());
        }

        isGameBusy = false;
        yield return null;
    }

    private IEnumerator MoveBoxes(GameObject box, Vector2 startPosition, Vector2 endPosition, float duration)
    {
        isGameBusy = true;

        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            float t = elapsedTime / duration;
            box.transform.position = Vector2.Lerp(startPosition, endPosition, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        box.transform.position = endPosition;

        isGameBusy = false;
        yield return null;
    }

    IEnumerator StartCoroutinesInSameFrame(List<IEnumerator> coroutines)
    {
        List<Coroutine> startedCoroutines = new List<Coroutine>();

        foreach (IEnumerator coroutine in coroutines)
        {
            startedCoroutines.Add(StartCoroutine(coroutine));
        }

        // Wait for all coroutines to finish
        foreach (Coroutine coroutine in startedCoroutines)
        {
            yield return coroutine;
        }
    }

    private bool IsObstacleAbove(int x, int y)
    {
        for (int i = y + 1; i < height; i++)
        {
            if (allObstacles[x, i] != null)
            {
                return true;
            }
        }
        return false;
    }

    private GameObject GetRandomBoxPrefab()
    {
        int randomIndex = UnityEngine.Random.Range(0, boxPrefabs.Count);
        return boxPrefabs[randomIndex];
    }

    private bool IsAdjacent(GameObject box1, GameObject box2)
    {
        Vector2Int box1Index = GetArrayIndexOfBox(box1);
        Vector2Int box2Index = GetArrayIndexOfBox(box2);

        int deltaX = Mathf.Abs(box1Index.x - box2Index.x);
        int deltaY = Mathf.Abs(box1Index.y - box2Index.y);

        return (deltaX == 1 && deltaY == 0) || (deltaX == 0 && deltaY == 1);
    }

    private Vector2Int GetArrayIndexOfBox(GameObject box)
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allBoxes[i, j] == box)
                {
                    return new Vector2Int(i, j);
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    public IEnumerator TrySwapBoxes(GameObject box1, GameObject box2)
    {
        if (isGameBusy) yield break;

        Vector2Int box1Index = GetArrayIndexOfBox(box1);
        Vector2Int box2Index = GetArrayIndexOfBox(box2);

        if (IsValidIndex(box1Index) && IsValidIndex(box2Index))
        {
            bool isAdjacent = IsAdjacent(box1, box2);

            if (isAdjacent)
            {
                yield return StartCoroutine(SwapBoxes(box1, box2, box1Index, box2Index));

                // Swap boxes in the matrix
                allBoxes[box1Index.x, box1Index.y] = box2;
                allBoxes[box2Index.x, box2Index.y] = box1;

                bool matchFound = CheckMatchesAtPosition(box1Index.x, box1Index.y) || CheckMatchesAtPosition(box2Index.x, box2Index.y);

                if (!matchFound)
                {
                    yield return StartCoroutine(SwapBoxes(box1, box2, box1Index, box2Index));

                    // Update matrix indexes again
                    allBoxes[box1Index.x, box1Index.y] = box1;
                    allBoxes[box2Index.x, box2Index.y] = box2;

                    selectedBox = null; // If no match was found, set the selected box to null
                }
                else
                {
                    yield return StartCoroutine(CheckMatchesAndRefill());
                }
            }
            else
            {
                Debug.Log("Boxes cannot be selected diagonally or are not adjacent.");
                selectedBox = box2; // Update selected box
                                    // Perform necessary actions here to highlight the selected box
            }
        }
        else
        {
            Debug.Log("Invalid box index.");
        }
    }

    private IEnumerator SwapBoxes(GameObject box1, GameObject box2, Vector2Int box1Index, Vector2Int box2Index)
    {
        isGameBusy = true;

        Vector2 box1Position = box1.transform.position;
        Vector2 box2Position = box2.transform.position;

        // Pozisyonları güncelle ve z koordinatını ayarla
        box1.transform.position = new Vector3(box1Position.x, box1Position.y, box2Index.y);
        box2.transform.position = new Vector3(box2Position.x, box2Position.y, box1Index.y);

        // Matristeki kutu yerlerini güncelle
        allBoxes[box1Index.x, box1Index.y] = box2;
        allBoxes[box2Index.x, box2Index.y] = box1;

        float elapsedTime = 0f;

        while (elapsedTime < boxSwapDuration)
        {
            float t = elapsedTime / boxSwapDuration;
            box1.transform.position = Vector3.Lerp(box1Position, box2Position, t);
            box2.transform.position = Vector3.Lerp(box2Position, box1Position, t);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        box1.transform.position = box2Position;
        box2.transform.position = box1Position;

        isGameBusy = false;
        yield return null;
    }

    private bool IsValidIndex(Vector2Int index)
    {
        return index.x >= 0 && index.x < width && index.y >= 0 && index.y < height;
    }

    public bool HasPossibleMatch()
    {
        int possibleMatchCount = 0;
        int totalDiagonalMatches = 0;
        int totalTripleMatches = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int firstPossibleMatch = CheckFirstPossibleMatch(x, y);
                possibleMatchCount += firstPossibleMatch;

                int diagonalMatches = CheckPossibleMatch(x, y);
                totalDiagonalMatches += diagonalMatches;

                int tripleMatches = CheckTriplePossibleMatch(x, y);
                totalTripleMatches += tripleMatches;
            }
        }

        Debug.Log("Total Possible Matches: " + possibleMatchCount);
        Debug.Log("Total Diagonal Matches: " + totalDiagonalMatches);
        Debug.Log("Total Triple Possible Matches: " + totalTripleMatches);

        // If no possible match found, return false
        return possibleMatchCount > 0 || totalDiagonalMatches > 0 || totalTripleMatches > 0;
    }

    private int CheckFirstPossibleMatch(int x, int y)
    {
        // Define the directions: left, up, right, down
        int[] dx = { 0, 0, -1, 1 };
        int[] dy = { -1, 1, 0, 0 };

        GameObject currentBox = allBoxes[x, y];
        if (currentBox == null || allObstacles[x, y] != null)
            return 0;

        Sprite currentSprite = currentBox.GetComponent<SpriteRenderer>().sprite;
        HashSet<Sprite> neighbourSprites = new HashSet<Sprite>();

        for (int dir = 0; dir < 4; dir++)
        {
            int nx = x + dx[dir];
            int ny = y + dy[dir];

            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                continue;

            GameObject neighbourBox = allBoxes[nx, ny];
            if (neighbourBox == null || allObstacles[nx, ny] != null)
                continue;

            Sprite neighbourSprite = neighbourBox.GetComponent<SpriteRenderer>().sprite;

            if (neighbourSprite == currentSprite)
                continue;

            neighbourSprites.Add(neighbourSprite);
        }

        int possibleMatchCount = 0;

        foreach (Sprite neighbourSprite in neighbourSprites)
        {
            int sameSpriteCount = 0;

            for (int dir = 0; dir < 4; dir++)
            {
                int nx = x + dx[dir];
                int ny = y + dy[dir];

                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                GameObject neighbourBox = allBoxes[nx, ny];
                if (neighbourBox == null || allObstacles[nx, ny] != null)
                    continue;

                Sprite neighbourOfNeighbourSprite = neighbourBox.GetComponent<SpriteRenderer>().sprite;

                if (neighbourOfNeighbourSprite == neighbourSprite)
                    sameSpriteCount++;
            }

            if (sameSpriteCount >= 3)
            {
                possibleMatchCount++;
            }
        }
        return possibleMatchCount;
    }

    private int CheckPossibleMatch(int x, int y)
    {
        int matchCount = 0;

        GameObject box1 = allBoxes[x, y];
        GameObject box2;

        if (x > 0 && x < width - 2 && allObstacles[x - 1, y] == null && allObstacles[x + 2, y] == null)
        {
            box2 = allBoxes[x + 1, y];
            if (box1 != null && box2 != null &&
                box1.GetComponent<SpriteRenderer>().sprite == box2.GetComponent<SpriteRenderer>().sprite)
            {
                if (y > 0 && x > 0 && allBoxes[x - 1, y - 1] != null &&
                    allBoxes[x - 1, y - 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (y < height - 1 && x > 0 && allBoxes[x - 1, y + 1] != null &&
                    allBoxes[x - 1, y + 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (y > 0 && x < width - 2 && allBoxes[x + 2, y - 1] != null &&
                    allBoxes[x + 2, y - 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (y < height - 1 && x < width - 2 && allBoxes[x + 2, y + 1] != null &&
                    allBoxes[x + 2, y + 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;
            }
        }

        if (y > 0 && y < height - 2 && allObstacles[x, y - 1] == null && allObstacles[x, y + 2] == null)
        {
            box2 = allBoxes[x, y + 1];
            if (box1 != null && box2 != null &&
                box1.GetComponent<SpriteRenderer>().sprite == box2.GetComponent<SpriteRenderer>().sprite)
            {
                if (x > 0 && y > 0 && allBoxes[x - 1, y - 1] != null &&
                    allBoxes[x - 1, y - 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (x < width - 1 && y > 0 && allBoxes[x + 1, y - 1] != null &&
                    allBoxes[x + 1, y - 1].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (x > 0 && y < height - 2 && allBoxes[x - 1, y + 2] != null &&
                    allBoxes[x - 1, y + 2].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;

                if (x < width - 1 && y < height - 2 && allBoxes[x + 1, y + 2] != null &&
                    allBoxes[x + 1, y + 2].GetComponent<SpriteRenderer>().sprite == box1.GetComponent<SpriteRenderer>().sprite)
                    matchCount++;
            }
        }
        return matchCount;
    }

    private int CheckTriplePossibleMatch(int x, int y)
    {
        int possibleMatchCount = 0;

        if (x < width - 3 && allObstacles[x + 2, y] == null)
        {
            GameObject box1 = allBoxes[x, y];
            GameObject box2 = allBoxes[x + 1, y];
            GameObject box4 = allBoxes[x + 3, y];

            if (box1 != null && box2 != null && box4 != null)
            {
                Sprite sprite1 = box1.GetComponent<SpriteRenderer>().sprite;
                Sprite sprite2 = box2.GetComponent<SpriteRenderer>().sprite;
                Sprite sprite4 = box4.GetComponent<SpriteRenderer>().sprite;

                if (sprite1 == sprite2 && sprite1 == sprite4)
                {
                    possibleMatchCount++;
                }
            }
        }

        if (y < height - 3 && allObstacles[x, y + 2] == null)
        {
            GameObject box1 = allBoxes[x, y];
            GameObject box2 = allBoxes[x, y + 1];
            GameObject box4 = allBoxes[x, y + 3];

            if (box1 != null && box2 != null && box4 != null)
            {
                Sprite sprite1 = box1.GetComponent<SpriteRenderer>().sprite;
                Sprite sprite2 = box2.GetComponent<SpriteRenderer>().sprite;
                Sprite sprite4 = box4.GetComponent<SpriteRenderer>().sprite;

                if (sprite1 == sprite2 && sprite1 == sprite4)
                {
                    possibleMatchCount++;
                }
            }
        }
        return possibleMatchCount;
    }

    private IEnumerator ShuffleBoard()
    {
        isGameBusy = true;       

        List<GameObject> boxes = new List<GameObject>();

        // Engeller olmayan ve dolu kutuları topla
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allObstacles[i, j] == null)
                {
                    GameObject box = allBoxes[i, j];
                    if (box != null)
                    {
                        boxes.Add(box);
                        allBoxes[i, j] = null;
                    }
                }
            }
        }

        // Kutuları karıştır
        System.Random random = new System.Random();
        int boxCount = boxes.Count;
        for (int i = 0; i < boxCount - 1; i++)
        {
            int randomIndex = random.Next(i, boxCount);
            GameObject temp = boxes[i];
            boxes[i] = boxes[randomIndex];
            boxes[randomIndex] = temp;
        }

        // Karıştırılmış kutuları engeller olmayan yerlere tahtaya yerleştir
        int boxIndex = 0;
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                if (allObstacles[i, j] == null)
                {
                    if (boxIndex < boxes.Count)
                    {
                        GameObject box = boxes[boxIndex];
                        allBoxes[i, j] = box;
                        Vector2 position = new Vector2(i + (boxSpacing.x * i), j + (boxSpacing.y * j));
                        box.transform.position = position;
                        boxIndex++;
                    }
                }
                if (boxIndex >= boxes.Count)
                {
                    break;
                }
            }
        }

        // Karıştırmadan sonra eşleşme kontrolü yap
        yield return StartCoroutine(CheckMatchesAndRefill());

        isGameBusy = false;
        yield return null;
    }

}