// ========================= NAMESPACE: Avonford_Secondary_School.Models.ViewModels =========================
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Avonford_Secondary_School.Models.ViewModels
{
    public class SellBookViewModel
    {
        // --- Form fields ---
        [Required(ErrorMessage = "Every epic tale needs a title. Drop yours here ✍️")]
        [StringLength(200, ErrorMessage = "Title is doing the most. Keep it under {1} characters.")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Who wrote this masterpiece? Author is required.")]
        [StringLength(200, ErrorMessage = "Author name is a tad long. Max {1} characters.")]
        public string Author { get; set; }

        [StringLength(100, ErrorMessage = "Edition looks a bit wordy. Max {1} characters.")]
        public string Edition { get; set; }

        [Required(ErrorMessage = "ISBN, please! We don’t accept mystery numbers.")]
        [RegularExpression(@"^(?:\d{9}[\dXx]|\d{13}|(?:97(8|9))?\d{9}[\dXx])$|^(97(8|9))?\d{1,5}([- ])\d{1,7}\1\d{1,7}\1[\dXx]$",
            ErrorMessage = "That ISBN looks shy. Use ISBN-10 or ISBN-13 (hyphens/spaces allowed).")]
        public string ISBN { get; set; }

        [Required(ErrorMessage = "Price can’t be zero vibes. Pop in a number > 0.")]
        [Range(0.01, 9999999, ErrorMessage = "Price must be greater than zero.")]
        public decimal Price { get; set; }


        [Display(Name = "Negotiable?")]
        public bool IsNegotiable { get; set; }

        [Required(ErrorMessage = "Pick a condition: New, Like New, Good, Fair, or Poor.")]
        [RegularExpression("^(New|Like New|Good|Fair|Poor)$", ErrorMessage = "Condition must be one of: New, Like New, Good, Fair, Poor.")]
        public string Condition { get; set; }

        [Required(ErrorMessage = "Give future readers the tea ☕. Description is required.")]
        [MinLength(300, ErrorMessage = "We love details! Description must be at least {1} characters.")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Where can the book be picked up? Address is required.")]
        [StringLength(300, ErrorMessage = "Address is too long. Max {1} characters.")]
        public string PickupAddress { get; set; }

        // These are set via the Google Maps autocomplete (client-side); optional but nice to have.
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        // --- Images ---
        [Required(ErrorMessage = "Your listing needs a face! Please upload a cover image.")]
        [AllowedExtensions(new[] { ".jpg", ".jpeg", ".png" }, ErrorMessage = "Cover image must be JPG or PNG.")]
        [MaxFileSize(3 * 1024 * 1024, ErrorMessage = "Cover image too thicc. Max 3MB.")]
        public HttpPostedFileBase CoverImage { get; set; }

        [MaxImagesCount(3, ErrorMessage = "You can add up to 3 extra pics, not a whole photo shoot 📸.")]
        public List<HttpPostedFileBase> ExtraImages { get; set; }

        // --- View helpers ---
        public string GoogleMapsApiKey { get; set; } // injected by controller for address autocomplete
    }

    public class BookPreviewViewModel
    {
        // Mirrors SellBookViewModel but with already-sanitized description + baked images (bytes) for preview
        public string Title { get; set; }
        public string Author { get; set; }
        public string Edition { get; set; }
        public string ISBN { get; set; }
        public decimal Price { get; set; }
        public bool IsNegotiable { get; set; }
        public string Condition { get; set; }
        public string DescriptionHtmlSafe { get; set; }
        public string PickupAddress { get; set; }
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }

        public UploadedImageVM CoverImage { get; set; }
        public List<UploadedImageVM> ExtraImages { get; set; } = new List<UploadedImageVM>();

        public int? TempBookIdForDisplay { get; set; } // not persisted; for fancy preview refs if needed
    }

    public class UploadedImageVM
    {
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public byte[] Bytes { get; set; }
        public bool IsMain { get; set; }
    }

    public class MyListingItemVM
    {
        public int BookID { get; set; }
        public string Title { get; set; }
        public string Condition { get; set; }
        public decimal Price { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MainImageBase64 { get; set; }
    }

    // -------------------- Custom Validation Attributes --------------------
    [AttributeUsage(AttributeTargets.Property)]
    public class MaxFileSize : ValidationAttribute
    {
        private readonly int _maxBytes;
        public MaxFileSize(int maxBytes) => _maxBytes = maxBytes;

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var file = value as HttpPostedFileBase;
            if (file == null) return ValidationResult.Success;
            if (file.ContentLength > _maxBytes)
                return new ValidationResult(ErrorMessage ?? $"File too large. Max {_maxBytes / (1024 * 1024)}MB");
            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class AllowedExtensions : ValidationAttribute
    {
        private readonly string[] _exts;
        public AllowedExtensions(string[] exts) => _exts = exts ?? Array.Empty<string>();

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var file = value as HttpPostedFileBase;
            if (file == null) return ValidationResult.Success;

            var ext = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? "";
            if (!_exts.Contains(ext))
                return new ValidationResult(ErrorMessage ?? $"Only {string.Join(", ", _exts)} allowed.");

            return ValidationResult.Success;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class MaxImagesCount : ValidationAttribute
    {
        private readonly int _max;
        public MaxImagesCount(int max) => _max = Math.Max(0, max);

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            var list = value as IEnumerable<HttpPostedFileBase>;
            if (list == null) return ValidationResult.Success;
            var count = list.Count(f => f != null && f.ContentLength > 0);
            if (count > _max)
                return new ValidationResult(ErrorMessage ?? $"Max {_max} images allowed.");
            return ValidationResult.Success;
        }
    }
}
