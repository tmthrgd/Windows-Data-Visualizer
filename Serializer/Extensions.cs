using System;
using System.Security.Cryptography;

namespace Com.Xenthrax.WindowsDataVisualizer.Serializer
{
	internal static class Extensions
	{
		public static bool ValidBlockSize(this SymmetricAlgorithm Algorithm, int bitLength)
		{
			KeySizes[] legalBlockSizes = Algorithm.LegalBlockSizes;

			if (legalBlockSizes != null)
			{
				for (int i = 0; i < legalBlockSizes.Length; i++)
				{
					if (legalBlockSizes[i].SkipSize == 0)
						if (legalBlockSizes[i].MinSize == bitLength)
							return true;
					else
						for (int j = legalBlockSizes[i].MinSize; j <= legalBlockSizes[i].MaxSize; j += legalBlockSizes[i].SkipSize)
							if (j == bitLength)
								return true;
				}
			}

			return false;
		}
	}
}