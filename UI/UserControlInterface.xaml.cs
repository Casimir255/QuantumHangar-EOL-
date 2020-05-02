using Newtonsoft.Json;
using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VRage.Game;
using VRage.ObjectBuilders;

namespace QuantumHangar.UI
{
    /// <summary>
    /// Interaction logic for UserControlInterface.xaml
    /// </summary>
    public partial class UserControlInterface : UserControl
    {
        private static readonly Logger Log = LogManager.GetLogger("QuantumHangarUI");
        //private bool GridLoaded = false;
        private Main Plugin { get; }



        public UserControlInterface()
        {
            InitializeComponent();
        }

        public UserControlInterface(Main plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;

            //plugin.PublicOffersAdded.CollectionChanged += PublicOffersAdded_CollectionChanged;

            plugin.Config.PublicOffers.CollectionChanged += PublicOffers_CollectionChanged;
        }

        private void PublicOffers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Main.Debug(e.Action.ToString());
            //Plugin.Config.RefreshModel();
            //throw new NotImplementedException();
        }

        private void TabController_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Grid_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            ContextMenu cm = this.FindResource("GridButton") as ContextMenu;
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }


        private void RefreshItems(object sender, RoutedEventArgs e)
        {
            


        }

        private void MenuItem_Click_1(object sender, RoutedEventArgs e)
        {
   
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //Refresh list
            string[] subdirectoryEntries = null;

            try
            {
                subdirectoryEntries = Directory.GetFiles(Main.ServerOffersDir, "*.sbc");
            }
            catch (Exception b)
            {
                //Prevent from continuing
                Main.Debug("Unable to get ServerOffersFolder!", b, Main.ErrorType.Fatal);
                return;
            }

            //Get only filenames
            string[] FolderGrids = new string[subdirectoryEntries.Count()];
            for(int i = 0; i< subdirectoryEntries.Count(); i++)
            {
                FolderGrids[i] = System.IO.Path.GetFileNameWithoutExtension(subdirectoryEntries[i]);
            }


            for(int i = 0; i< Plugin.Config.PublicOffers.Count; i++)
            {
                if (!FolderGrids.Contains(Plugin.Config.PublicOffers[i].Name))
                {
                    Plugin.Config.PublicOffers.RemoveAt(i);
                }
            }


            for(int i = 0; i < FolderGrids.Count(); i++)
            {
                if(!Plugin.Config.PublicOffers.Any(x=> x.Name == FolderGrids[i])){

                    string FileName = FolderGrids[i];

                    Main.Debug("Adding item: " + FileName);

                    PublicOffers offer = new PublicOffers();
                    offer.Name = FileName;
                    Plugin.Config.PublicOffers.Add(offer);
                    Plugin.Config.RefreshModel();
                }
            }

        }

        private void Imported_LostFocus(object sender, RoutedEventArgs e)
        {
            Plugin.Config.RefreshModel();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var t = Task.Run(() => UpdatePublicOffers());
            //This will force the market to update the market items
        }

        private void UpdatePublicOffers()
        {

            //Update all public offer market items!
            if(!Main.IsRunning)
            {
                return;
            }

            //Need to remove existing ones
            for (int i = 0; i < Main.GridList.Count; i++)
            {
                if (Main.GridList[i].Steamid != 0)
                    continue;


                Main.Debug("Removing public offer: " + Main.GridList[i].Name);
                Main.GridList.RemoveAt(i);
            }

            string PublicOfferPath = System.IO.Path.Combine(Main.Dir, "PublicOffers");

            //Clear list
            Main.PublicOfferseGridList.Clear();


            foreach (PublicOffers offer in Plugin.Config.PublicOffers)
            {
                if (offer.Forsale && (offer.Name != null || offer.Name != ""))
                {
                    string GridFilePath = System.IO.Path.Combine(PublicOfferPath, offer.Name + ".sbc");


                    MyObjectBuilderSerializer.DeserializeXML(GridFilePath, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);
                    MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprint = null;
                    try
                    {
                        shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                    }
                    catch
                    {
                        Main.Debug("Error on BP: "+ offer.Name + "! Most likely you put in the SBC5 file! Dont do that!");
                        continue;
                    }
                    MyObjectBuilder_CubeGrid grid = shipBlueprint[0].CubeGrids[0];
                    byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);

                    //HangarChecks.GetPublicOfferBPDetails(shipBlueprint, out GridStamp stamp);


                    //Straight up add new ones
                    MarketList NewList = new MarketList();
                    NewList.Steamid = 0;
                    NewList.Name = offer.Name;
                    NewList.Seller = offer.Seller;
                    NewList.SellerFaction = offer.SellerFaction;
                    NewList.Price = offer.Price;
                    NewList.Description = offer.Description;
                    NewList.GridDefinition = Definition;
                    //NewList.GridBuiltPercent = stamp.GridBuiltPercent;
                    //NewList.NumberofBlocks = stamp.NumberofBlocks;
                    //NewList.SmallGrids = stamp.SmallGrids;
                    //NewList.LargeGrids = stamp.LargeGrids;
                    // NewList.BlockTypeCount = stamp.BlockTypeCount;

                    //Need to setTotalBuys
                    Main.PublicOfferseGridList.Add(NewList);

                   Main.Debug("Adding new public offer: " + offer.Name);
                }
            }


            //Update Everything!
            Comms.SendListToModOnInitilize();

            MarketData Data = new MarketData();
            Data.List = Main.PublicOfferseGridList;

            //Write to file
            FileSaver.Save(Main.ServerMarketFileDir, Data);
            //File.WriteAllText(Main.ServerMarketFileDir, JsonConvert.SerializeObject(Data));
            //This will force the market to update the market items
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;

            if(button.Name == "FixedPrice")
            {
                StaticGBox.Visibility = Visibility.Visible;
                StaticGText.Visibility = Visibility.Visible;
                StaticGText.Text = "Fixed Price Amount:";

                LargeGBox.Visibility = Visibility.Hidden;
                LargeGText.Visibility = Visibility.Hidden;

                SmallGBox.Visibility = Visibility.Hidden;
                SmallGText.Visibility = Visibility.Hidden;


            }
            else
            {
                StaticGBox.Visibility = Visibility.Visible;
                StaticGText.Visibility = Visibility.Visible;
                StaticGText.Text = "Static Grid Currency:";

                LargeGBox.Visibility = Visibility.Visible;
                LargeGText.Visibility = Visibility.Visible;

                SmallGBox.Visibility = Visibility.Visible;
                SmallGText.Visibility = Visibility.Visible;
            }
        }
    }

}

namespace QuantumHangar.Converters
{
    public class EnumBooleanConverter : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            return Enum.Parse(targetType, parameterString);
        }
        #endregion
    }

    
    public class TextToBoolConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                return !(bool)value;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                return !(bool)value;
            }
            return value;
        }

        #endregion
    }
    

}
