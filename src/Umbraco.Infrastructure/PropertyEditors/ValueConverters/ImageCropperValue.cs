// Copyright (c) Umbraco.
// See LICENSE for more details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Serialization;
using Umbraco.Extensions;

namespace Umbraco.Cms.Core.PropertyEditors.ValueConverters
{
    /// <summary>
    /// Represents a value of the image cropper value editor.
    /// </summary>
    [JsonConverter(typeof(NoTypeConverterJsonConverter<ImageCropperValue>))]
    [TypeConverter(typeof(ImageCropperValueTypeConverter))]
    [DataContract(Name="imageCropDataSet")]
    public class ImageCropperValue : IHtmlEncodedString, IEquatable<ImageCropperValue>
    {
        /// <summary>
        /// Gets or sets the value source image.
        /// </summary>
        [DataMember(Name="src")]
        public string Src { get; set;}

        /// <summary>
        /// Gets or sets the value focal point.
        /// </summary>
        [DataMember(Name = "focalPoint")]
        public ImageCropperFocalPoint FocalPoint { get; set; }

        /// <summary>
        /// Gets or sets the value crops.
        /// </summary>
        [DataMember(Name = "crops")]
        public IEnumerable<ImageCropperCrop> Crops { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return Crops != null ? (Crops.Any() ? JsonConvert.SerializeObject(this) : Src) : string.Empty;
        }

        /// <inheritdoc />
        public string ToHtmlString() => Src;

        /// <summary>
        /// Gets a crop.
        /// </summary>
        public ImageCropperCrop GetCrop(string alias)
        {
            if (Crops == null)
                return null;

            return string.IsNullOrWhiteSpace(alias)
                ? Crops.FirstOrDefault()
                : Crops.FirstOrDefault(x => x.Alias.InvariantEquals(alias));
        }

        public ImageUrlGenerationOptions GetCropBaseOptions(string url, ImageCropperCrop crop, bool preferFocalPoint)
        {
            if ((preferFocalPoint && HasFocalPoint()) || (crop != null && crop.Coordinates == null && HasFocalPoint()))
            {
                return new ImageUrlGenerationOptions(url) { FocalPoint = new ImageUrlGenerationOptions.FocalPointPosition(FocalPoint.Left, FocalPoint.Top) };
            }
            else if (crop != null && crop.Coordinates != null && preferFocalPoint == false)
            {
                return new ImageUrlGenerationOptions(url) { Crop = new ImageUrlGenerationOptions.CropCoordinates(crop.Coordinates.X1, crop.Coordinates.Y1, crop.Coordinates.X2, crop.Coordinates.Y2) };
            }
            else
            {
                return new ImageUrlGenerationOptions(url);
            }
        }

        /// <summary>
        /// Gets the value image URL for a specified crop.
        /// </summary>
        public string GetCropUrl(string alias, IImageUrlGenerator imageUrlGenerator, bool useCropDimensions = true, bool useFocalPoint = false, string cacheBusterValue = null)
        {
            var crop = GetCrop(alias);

            // could not find a crop with the specified, non-empty, alias
            if (crop == null && !string.IsNullOrWhiteSpace(alias))
                return null;

            var options = GetCropBaseOptions(null, crop, useFocalPoint || string.IsNullOrWhiteSpace(alias));

            if (crop != null && useCropDimensions)
            {
                options.Width = crop.Width;
                options.Height = crop.Height;
            }

            options.CacheBusterValue = cacheBusterValue;

            return imageUrlGenerator.GetImageUrl(options);
        }

        /// <summary>
        /// Gets the value image URL for a specific width and height.
        /// </summary>
        public string GetCropUrl(int width, int height, IImageUrlGenerator imageUrlGenerator, string cacheBusterValue = null)
        {
            var options = GetCropBaseOptions(null, null, false);

            options.Width = width;
            options.Height = height;
            options.CacheBusterValue = cacheBusterValue;

            return imageUrlGenerator.GetImageUrl(options);
        }

        /// <summary>
        /// Determines whether the value has a focal point.
        /// </summary>
        /// <returns></returns>
        public bool HasFocalPoint()
            => FocalPoint != null && (FocalPoint.Left != 0.5m || FocalPoint.Top != 0.5m);

        /// <summary>
        /// Determines whether the value has a specified crop.
        /// </summary>
        public bool HasCrop(string alias)
            => Crops != null && Crops.Any(x => x.Alias == alias);

        /// <summary>
        /// Determines whether the value has a source image.
        /// </summary>
        public bool HasImage()
            => !string.IsNullOrWhiteSpace(Src);

        public ImageCropperValue Merge(ImageCropperValue imageCropperValue)
        {
            var crops = Crops?.ToList() ?? new List<ImageCropperCrop>();

            var incomingCrops = imageCropperValue?.Crops;
            if (incomingCrops != null)
            {
                foreach (var incomingCrop in incomingCrops)
                {
                    var crop = crops.FirstOrDefault(x => x.Alias == incomingCrop.Alias);
                    if (crop == null)
                    {
                        // Add incoming crop
                        crops.Add(incomingCrop);
                    }
                    else if (crop.Coordinates == null)
                    {
                        // Use incoming crop coordinates
                        crop.Coordinates = incomingCrop.Coordinates;
                    }
                }
            }

            return new ImageCropperValue()
            {
                Src = !string.IsNullOrWhiteSpace(Src) ? Src : imageCropperValue?.Src,
                Crops = crops,
                FocalPoint = FocalPoint ?? imageCropperValue?.FocalPoint
            };
        }

        #region IEquatable

        /// <inheritdoc />
        public bool Equals(ImageCropperValue other)
            => ReferenceEquals(this, other) || Equals(this, other);

        /// <inheritdoc />
        public override bool Equals(object obj)
            => ReferenceEquals(this, obj) || obj is ImageCropperValue other && Equals(this, other);

        private static bool Equals(ImageCropperValue left, ImageCropperValue right)
            => ReferenceEquals(left, right) // deals with both being null, too
                || !ReferenceEquals(left, null) && !ReferenceEquals(right, null)
                   && string.Equals(left.Src, right.Src)
                   && Equals(left.FocalPoint, right.FocalPoint)
                   && left.ComparableCrops.SequenceEqual(right.ComparableCrops);

        private IEnumerable<ImageCropperCrop> ComparableCrops
            => Crops?.OrderBy(x => x.Alias) ?? Enumerable.Empty<ImageCropperCrop>();

        public static bool operator ==(ImageCropperValue left, ImageCropperValue right)
            => Equals(left, right);

        public static bool operator !=(ImageCropperValue left, ImageCropperValue right)
            => !Equals(left, right);

        public override int GetHashCode()
        {
            unchecked
            {
                // properties are, practically, readonly
                // ReSharper disable NonReadonlyMemberInGetHashCode
                var hashCode = Src?.GetHashCode() ?? 0;
                hashCode = (hashCode*397) ^ (FocalPoint?.GetHashCode() ?? 0);
                hashCode = (hashCode*397) ^ (Crops?.GetHashCode() ?? 0);
                return hashCode;
                // ReSharper restore NonReadonlyMemberInGetHashCode
            }
        }

        #endregion

        [DataContract(Name = "imageCropFocalPoint")]
        public class ImageCropperFocalPoint : IEquatable<ImageCropperFocalPoint>
        {
            [DataMember(Name = "left")]
            public decimal Left { get; set; }

            [DataMember(Name = "top")]
            public decimal Top { get; set; }

            #region IEquatable

            /// <inheritdoc />
            public bool Equals(ImageCropperFocalPoint other)
                => ReferenceEquals(this, other) || Equals(this, other);

            /// <inheritdoc />
            public override bool Equals(object obj)
                => ReferenceEquals(this, obj) || obj is ImageCropperFocalPoint other && Equals(this, other);

            private static bool Equals(ImageCropperFocalPoint left, ImageCropperFocalPoint right)
                => ReferenceEquals(left, right) // deals with both being null, too
                   || !ReferenceEquals(left, null) && !ReferenceEquals(right, null)
                       && left.Left == right.Left
                       && left.Top == right.Top;

            public static bool operator ==(ImageCropperFocalPoint left, ImageCropperFocalPoint right)
                => Equals(left, right);

            public static bool operator !=(ImageCropperFocalPoint left, ImageCropperFocalPoint right)
                => !Equals(left, right);

            public override int GetHashCode()
            {
                unchecked
                {
                    // properties are, practically, readonly
                    // ReSharper disable NonReadonlyMemberInGetHashCode
                    return (Left.GetHashCode()*397) ^ Top.GetHashCode();
                    // ReSharper restore NonReadonlyMemberInGetHashCode
                }
            }

            #endregion
        }

        [DataContract(Name = "imageCropData")]
        public class ImageCropperCrop : IEquatable<ImageCropperCrop>
        {
            [DataMember(Name = "alias")]
            public string Alias { get; set; }

            [DataMember(Name = "width")]
            public int Width { get; set; }

            [DataMember(Name = "height")]
            public int Height { get; set; }

            [DataMember(Name = "coordinates")]
            public ImageCropperCropCoordinates Coordinates { get; set; }

            #region IEquatable

            /// <inheritdoc />
            public bool Equals(ImageCropperCrop other)
                => ReferenceEquals(this, other) || Equals(this, other);

            /// <inheritdoc />
            public override bool Equals(object obj)
                => ReferenceEquals(this, obj) || obj is ImageCropperCrop other && Equals(this, other);

            private static bool Equals(ImageCropperCrop left, ImageCropperCrop right)
                => ReferenceEquals(left, right) // deals with both being null, too
                    || !ReferenceEquals(left, null) && !ReferenceEquals(right, null)
                       && string.Equals(left.Alias, right.Alias)
                       && left.Width == right.Width
                       && left.Height == right.Height
                       && Equals(left.Coordinates, right.Coordinates);

            public static bool operator ==(ImageCropperCrop left, ImageCropperCrop right)
                => Equals(left, right);

            public static bool operator !=(ImageCropperCrop left, ImageCropperCrop right)
                => !Equals(left, right);

            public override int GetHashCode()
            {
                unchecked
                {
                    // properties are, practically, readonly
                    // ReSharper disable NonReadonlyMemberInGetHashCode
                    var hashCode = Alias?.GetHashCode() ?? 0;
                    hashCode = (hashCode*397) ^ Width;
                    hashCode = (hashCode*397) ^ Height;
                    hashCode = (hashCode*397) ^ (Coordinates?.GetHashCode() ?? 0);
                    return hashCode;
                    // ReSharper restore NonReadonlyMemberInGetHashCode
                }
            }

            #endregion
        }

        [DataContract(Name = "imageCropCoordinates")]
        public class ImageCropperCropCoordinates : IEquatable<ImageCropperCropCoordinates>
        {
            [DataMember(Name = "x1")]
            public decimal X1 { get; set; }

            [DataMember(Name = "y1")]
            public decimal Y1 { get; set; }

            [DataMember(Name = "x2")]
            public decimal X2 { get; set; }

            [DataMember(Name = "y2")]
            public decimal Y2 { get; set; }

            #region IEquatable

            /// <inheritdoc />
            public bool Equals(ImageCropperCropCoordinates other)
                => ReferenceEquals(this, other) || Equals(this, other);

            /// <inheritdoc />
            public override bool Equals(object obj)
                => ReferenceEquals(this, obj) || obj is ImageCropperCropCoordinates other && Equals(this, other);

            private static bool Equals(ImageCropperCropCoordinates left, ImageCropperCropCoordinates right)
                => ReferenceEquals(left, right) // deals with both being null, too
                   || !ReferenceEquals(left, null) && !ReferenceEquals(right, null)
                      && left.X1 == right.X1
                      && left.X2 == right.X2
                      && left.Y1 == right.Y1
                      && left.Y2 == right.Y2;

            public static bool operator ==(ImageCropperCropCoordinates left, ImageCropperCropCoordinates right)
                => Equals(left, right);

            public static bool operator !=(ImageCropperCropCoordinates left, ImageCropperCropCoordinates right)
                => !Equals(left, right);

            public override int GetHashCode()
            {
                unchecked
                {
                    // properties are, practically, readonly
                    // ReSharper disable NonReadonlyMemberInGetHashCode
                    var hashCode = X1.GetHashCode();
                    hashCode = (hashCode*397) ^ Y1.GetHashCode();
                    hashCode = (hashCode*397) ^ X2.GetHashCode();
                    hashCode = (hashCode*397) ^ Y2.GetHashCode();
                    return hashCode;
                    // ReSharper restore NonReadonlyMemberInGetHashCode
                }
            }

            #endregion
        }
    }
}
