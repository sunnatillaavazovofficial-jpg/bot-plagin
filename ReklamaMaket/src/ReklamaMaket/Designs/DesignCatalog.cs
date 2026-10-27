namespace ReklamaMaket.Designs
{
    /// <summary>
    /// Paneldagi "Dizayn turi" ro'yxati. Yangi tur qo'shganda shu yerga bitta qator yozing.
    /// </summary>
    public static class DesignCatalog
    {
        public static readonly DesignType[] All =
        {
            new LightboxDesign(),
            new BannerDesign(),
            new BillboardDesign(),
            new PylonDesign(),
        };
    }
}
