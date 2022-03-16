namespace DispSupport
{
    /// <summary>
    /// Класс описывающий общий объект режима
    /// </summary>
    public class ModeObject
    { }

    /// <summary>
    /// Класс, описывающий НПС
    /// </summary>
    class PumpStation : ModeObject
    {
        public int Number { get; set; }
        public int MPUCount { get; set; }
        public int SPUCount { get; set; }
        public double UstPin { get; set; }
        public double UstPout { get; set; }
        public int PUStatus { get; set; }
    }
    /// <summary>
    /// Класс, описывающий регулятор давления
    /// </summary>
    class PressureRegulator : ModeObject
    {
        public double UstPin { get; set; }
    }
}
