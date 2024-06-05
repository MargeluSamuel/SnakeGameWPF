using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static SnakeGameWPF.MainWindow;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Serialization;
using System.IO.Compression;

namespace SnakeGameWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public ObservableCollection<Player> _leaderboardList { get; set; } = new ObservableCollection<Player>();

        const int _snakeSquareSize = 20;
        
        private System.Windows.Threading.DispatcherTimer gameTickTimer = new System.Windows.Threading.DispatcherTimer();

        private SolidColorBrush _snakeBodyPartBrush = Brushes.Green;
        private SolidColorBrush _snakeHeadBrush = Brushes.GreenYellow;
        private SolidColorBrush _foodBrush = Brushes.Yellow;
        private List <SnakePart> _snakeParts = new List <SnakePart>();

        private const int _maxLeaderboardEntries = 5;

        private UIElement _snakeFood = null;

        public enum _snakeDirections { Left, Right, Up, Down };
        private _snakeDirections _currentSnakeDirection = _snakeDirections.Right;
        private int _snakeLength; //currentSnakeLength
        private int _startingScore = 0;
        private int _currentScore = 0;

        private Random rand = new Random();

        const int _snakeStartLength = 3;
        const int _snakeStartSpeed = 400;
        const int _snakeSpeedThreshold = 100;

        public Dictionary<_snakeDirections, Tuple<int, int>> _movementValues = new Dictionary<_snakeDirections, Tuple<int, int>>()
        {
            {_snakeDirections.Left, new Tuple<int, int> (-_snakeSquareSize, 0) },
            {_snakeDirections.Right, new Tuple<int, int>(_snakeSquareSize, 0) },
            {_snakeDirections.Up, new Tuple<int, int> (0, -_snakeSquareSize) },
            {_snakeDirections.Down, new Tuple<int, int> (0, _snakeSquareSize) }
        };

        public MainWindow()
        {
            InitializeComponent();
            gameTickTimer.Tick += GameTickTimer_Tick;
            LoadLeaderboard();
        }

        private void LoadLeaderboard()
        {
            if (File.Exists("Leaderboard.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<Player>));
                using (Stream stream = new FileStream("Leaderboard.xml", FileMode.Open))
                {
                    List<Player> tempLeaderboard = (List<Player>)serializer.Deserialize(stream);
                    this._leaderboardList.Clear();
                    foreach (Player player in tempLeaderboard.OrderByDescending(x => x._score))
                    {
                        _leaderboardList.Add(player);
                    }
                }
            }
        }

        private void SaveLeaderboard()
        {
            XmlSerializer serializer = new XmlSerializer (typeof(ObservableCollection<Player>));

            using (Stream sout = new FileStream("Leaderboard.xml", FileMode.Create)) {
                serializer.Serialize(sout, this._leaderboardList);
            }
        }
        private void GameTickTimer_Tick(object sender, EventArgs e)
        {
            moveSnake();
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            DrawGameArea();
        }

        private void DrawGameArea()
        {
            bool doneDrawing = false;
            int nextX = 0, nextY = 0, rowCounter = 0;
            bool nextIsOdd = false;

            while (!doneDrawing)
            {
                //Creating and choosing current color
                SolidColorBrush color;

                if (nextIsOdd)
                {
                    color = Brushes.White;
                }
                else
                {
                    color = Brushes.Black;
                }

                Rectangle rect = new Rectangle
                {
                    Width = _snakeSquareSize,
                    Height = _snakeSquareSize,
                    Fill = color
                };

                GameArea.Children.Add(rect);
                Canvas.SetTop(rect, nextY);
                Canvas.SetLeft(rect, nextX);

                nextIsOdd = !nextIsOdd;

                nextX += _snakeSquareSize;

                if (nextX >= GameArea.ActualWidth)
                {
                    nextX = 0;
                    nextY += _snakeSquareSize;
                    rowCounter++;

                    nextIsOdd = (rowCounter % 2 != 0);
                }

                if (nextY >= GameArea.ActualHeight)
                {
                    doneDrawing = true;
                }

            }
        }

        private void DrawSnake()
        {
            foreach (SnakePart snakePart in _snakeParts)
            {
                if (snakePart._UIElement == null)
                {
                    Rectangle rect = new Rectangle
                    {
                        Width = _snakeSquareSize,
                        Height = _snakeSquareSize,
                        Fill = snakePart._isHead ? _snakeHeadBrush : _snakeBodyPartBrush
                    };

                    snakePart._UIElement = rect;

                    GameArea.Children.Add(rect);
                    Canvas.SetTop(snakePart._UIElement, snakePart._position.Y);
                    Canvas.SetLeft(snakePart._UIElement, snakePart._position.X);
                }
            }
        }

        private void DrawSnakeFood()
        {
            Point foodLocation = nextFoodLocation();

            _snakeFood = new Ellipse()
            {
                Width = _snakeSquareSize,
                Height = _snakeSquareSize,
                Fill = _foodBrush,
                Stroke = Brushes.Purple,
                StrokeThickness = 2
            };

            GameArea.Children.Add(_snakeFood);
            Canvas.SetTop(_snakeFood, foodLocation.Y);
            Canvas.SetLeft(_snakeFood, foodLocation.X);
        }

        private Point nextFoodLocation()
        {
            int foodX, foodY, maxX, maxY;

            maxX = (int)(GameArea.ActualWidth / _snakeSquareSize);
            maxY = (int)(GameArea.ActualHeight / _snakeSquareSize);


            foodX = rand.Next(0, maxX) * _snakeSquareSize;
            foodY = rand.Next(0, maxY) * _snakeSquareSize; 

            foreach (SnakePart snakePart in _snakeParts)
            {
                if ((foodX == snakePart._position.X) && (foodY == snakePart._position.Y))
                {
                    return nextFoodLocation();
                }
            }

            return new Point(foodX, foodY);
        }

        private void SnakeEatFood()
        {
            _currentScore++;
            _snakeLength++;
            int timerInterval = Math.Max(_snakeSpeedThreshold, (int)gameTickTimer.Interval.TotalMilliseconds - (_currentScore * 2));
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(timerInterval);

            GameArea.Children.Remove(_snakeFood);
            DrawSnakeFood();
            UpdateGameStatus();
        }

        private void moveSnake()
        {
            while (_snakeParts.Count > _snakeLength)
            {
                GameArea.Children.Remove(_snakeParts[0]._UIElement);
                _snakeParts.RemoveAt(0);
            }

            foreach (SnakePart snakePart in _snakeParts )
            {
                (snakePart._UIElement as Rectangle).Fill = _snakeBodyPartBrush;
                snakePart._isHead = false;
            }

            SnakePart prevSnakeHead = _snakeParts[_snakeParts.Count - 1];

            double nextX = prevSnakeHead._position.X + _movementValues[_currentSnakeDirection].Item1;
            double nextY = prevSnakeHead._position.Y + _movementValues[_currentSnakeDirection].Item2;

            _snakeParts.Add(new SnakePart()
            {
                _position = new Point(nextX, nextY),
                _isHead = true
            });

            DrawSnake();

            CollisionCheck();
        }

        private void StartNewGame()
        {

            borderWelcomeMessage.Visibility = Visibility.Collapsed;
            borderLeaderboard.Visibility = Visibility.Collapsed;
            borderGameOver.Visibility = Visibility.Collapsed;

            foreach (SnakePart snakePart in _snakeParts)
            {
                GameArea.Children.Remove(snakePart._UIElement);
            }

            _snakeParts.Clear();

            if (_snakeFood != null )
            {
                GameArea.Children.Remove(_snakeFood);
            }


            _currentScore = 0;
            _snakeLength = _snakeStartLength;
            _currentSnakeDirection = _snakeDirections.Right;
            _snakeParts.Add(new SnakePart() { _position = new Point(_snakeSquareSize * 5, _snakeSquareSize * 5) });
            gameTickTimer.Interval = TimeSpan.FromMilliseconds(_snakeStartSpeed);

            // Draw the snake  
            DrawSnake();

            DrawSnakeFood();

            // Go!          
            gameTickTimer.IsEnabled = true;
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            _snakeDirections originalSnakeDirection = _currentSnakeDirection;

            switch (e.Key)
            {
                case Key.Up or Key.W:
                    if (_currentSnakeDirection != _snakeDirections.Down)
                    {
                        _currentSnakeDirection = _snakeDirections.Up;
                    }
                    break;
                case Key.Down or Key.S:
                    if (_currentSnakeDirection != _snakeDirections.Up)
                    {
                        _currentSnakeDirection = _snakeDirections.Down;
                    }
                    break;
                case Key.Left or Key.A:
                    if (_currentSnakeDirection != _snakeDirections.Right)
                    {
                        _currentSnakeDirection = _snakeDirections.Left;
                    }
                    break;
                case Key.Right or Key.D:
                    if (_currentSnakeDirection != _snakeDirections.Left)
                    {
                        _currentSnakeDirection = _snakeDirections.Right;
                    }
                    break;
                case Key.Space or Key.R:
                    StartNewGame();
                    break;
            }

            if (_currentSnakeDirection != originalSnakeDirection)
            {
                moveSnake();
            }
        }

        private void CollisionCheck()
        {
            SnakePart snakeHead = _snakeParts[_snakeParts.Count - 1];

            //check if snake eat food

            if ((snakeHead._position.X == Canvas.GetLeft(_snakeFood)) && (snakeHead._position.Y == Canvas.GetTop(_snakeFood))) {
                SnakeEatFood();
                return;
            }

            //check if snake is out of borders

            if ((snakeHead._position.X < 0) || (snakeHead._position.X >= GameArea.ActualWidth)
                || (snakeHead._position.Y < 0) || (snakeHead._position.Y >= GameArea.ActualHeight))
            {
                EndGame();
            }

            foreach (SnakePart snakePart in _snakeParts.Take(_snakeParts.Count - 1))
            {
                if ((snakeHead._position.X == snakePart._position.X) && (snakeHead._position.Y == snakePart._position.Y))
                {
                    EndGame();
                }
            }
        }


        private void UpdateGameStatus()
        {
            this.tbScore.Text = _currentScore.ToString();
            this.tbSpeed.Text = gameTickTimer.Interval.TotalMilliseconds.ToString();
        }

        private void EndGame()
        {
            bool newHighscore = false;

            if (_currentScore > 0)
            {
                int lowestScore = (this._leaderboardList.Count > 0 ? this._leaderboardList.Min(x => x._score) : 0);

                if ((_currentScore > lowestScore) || (this._leaderboardList.Count < _maxLeaderboardEntries))
                {
                    borderNewHighScore.Visibility = Visibility.Visible;
                    textBoxPlayerName.Focus();
                    newHighscore = true;
                }
            }

            if (!newHighscore)
            {

                textBlockFinalScore.Text = _currentScore.ToString();
                borderGameOver.Visibility = Visibility.Visible;
            }

            gameTickTimer.IsEnabled = false;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnShowLeaderboard_Click(object sender, RoutedEventArgs e)
        {
            borderLeaderboard.Visibility = Visibility.Visible;
            borderWelcomeMessage.Visibility = Visibility.Collapsed;
        }

        private void btnSubmitHighscore_Click(object sender, RoutedEventArgs e)
        {
            int newIndex = 0;

            if ((this._leaderboardList.Count > 0) && (_currentScore < this._leaderboardList.Max(x => x._score)))
            {
                //current player's score will be placed right under "abovePlayer"
                Player abovePlayer = this._leaderboardList.OrderByDescending(x => x._score).First(x => x._score >= _currentScore);

                if (abovePlayer != null)
                {
                    newIndex = this._leaderboardList.IndexOf(abovePlayer) + 1;
                }
            }

            this._leaderboardList.Insert(newIndex, new Player()
            {
                _name = textBoxPlayerName.Text,
                _score = _currentScore
            });

            while (this._leaderboardList.Count > _maxLeaderboardEntries)
            {
                this._leaderboardList.RemoveAt(_maxLeaderboardEntries);
            }

            SaveLeaderboard();

            borderNewHighScore.Visibility = Visibility.Collapsed;
            borderLeaderboard.Visibility = Visibility.Visible;
        }
    }
}