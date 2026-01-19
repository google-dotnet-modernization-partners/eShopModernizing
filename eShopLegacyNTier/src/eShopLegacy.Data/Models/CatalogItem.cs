namespace eShopLegacy.Data.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;
    using System.Runtime.Serialization;

    [DataContract]
    public partial class CatalogItem
    {
        public const string DefaultPictureName = "dummy.png";

        public CatalogItem()
        {
            PictureFileName = DefaultPictureName;
        }

        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [DataMember]
        public int Id { get; set; }

        [DataMember]
        public string Description { get; set; }

        [Required]
        [DataMember]
        public string Name { get; set; }

        [Column(TypeName = "money")]
        [DataMember]
        [Range(0, 1000000)]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "Picture name")]
        [DataMember]
        public string PictureFileName { get; set; }

        [DataMember]
        public string PictureUri { get; set; }

        [DataMember]
        [Display(Name = "Brand")]
        public int CatalogBrandId { get; set; }

        [DataMember]
        [Display(Name = "Type")]
        public int CatalogTypeId { get; set; }

        [DataMember]
        [Display(Name = "Type")]
        public CatalogType CatalogType { get; set; }

        [DataMember]
        [Display(Name = "Brand")]
        public CatalogBrand CatalogBrand { get; set; }

        [NotMapped]
        [DataMember]
        public decimal? DiscountedPrice { get; set; }

        // Quantity in stock
        [Range(0, 10000000, ErrorMessage = "The field Stock must be between 0 and 10 million.")]
        [Display(Name = "Stock")]
        [DataMember]
        public int AvailableStock { get; set; }

        // Available stock at which we should reorder
        [Range(0, 10000000, ErrorMessage = "The field Restock must be between 0 and 10 million.")]
        [Display(Name = "Restock")]
        [DataMember]
        public int RestockThreshold { get; set; }

        // Maximum number of units that can be in-stock at any time (due to physicial/logistical constraints in warehouses)
        [Range(0, 10000000, ErrorMessage = "The field Max stock must be between 0 and 10 million.")]
        [Display(Name = "Max stock")]
        [DataMember]
        public int MaxStockThreshold { get; set; }

        /// <summary>
        /// True if item is on reorder
        /// </summary>
        [DataMember]
        public bool OnReorder { get; set; }
    }
}