using System;

namespace wrpper_test
{
    class Program
    {
        static void Main(string[] args)
        {
            using(var wrapper = new ExifToolUtils.StayOpenWrapper(@"C:\Scratch\Image_Processing\exiftool\exiftool.exe"))
            {
                var result = wrapper.Execute(new [] {"-xmp", "-b", @"C:\Scratch\Image_Processing\test.JPG"});
                result = wrapper.Execute(new [] {"-xmp", "-b", @"C:\Scratch\Image_Processing\IMG_2123.JPG"});
                result = wrapper.Execute(new [] {"-xmp", "-b", "foo.jpg"});
            }
        }
    }
}
