﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CmsData.Classes.DataMapper;

namespace CmsWeb.Areas.Public.Models.CheckInAPIv2
{
	[SuppressMessage( "ReSharper", "CollectionNeverQueried.Global" )]
	[SuppressMessage( "ReSharper", "UnusedMember.Global" )]
	[SuppressMessage( "ReSharper", "NotAccessedField.Global" )]
	[SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" )]
	public class Family : DataMapper
	{
		public int id = 0;
		public string name = "";

		public string picture = "";

		public bool locked = false;

		public readonly List<FamilyMember> members = new List<FamilyMember>();

		public static List<Family> forSearch( SqlConnection db, string search, int campus, DateTime date )
		{
			List<Family> families = new List<Family>();
			DataTable table = new DataTable();

			const string qFamilies = @"SELECT family.FamilyId AS id,
		 												MAX( head.Name ) AS name,
		 												CAST( CASE WHEN MAX( lock.FamilyId ) IS NOT NULL THEN 1 ELSE 0 END AS bit ) AS locked
		 											FROM dbo.Families family
		 												LEFT JOIN dbo.People AS members
		 													ON family.FamilyId = members.FamilyId
		 												LEFT JOIN dbo.People AS head
		 													ON family.HeadOfHouseholdId = head.PeopleId
		 														AND head.DeceasedDate IS NULL
		 												LEFT JOIN dbo.PeopleExtra AS extras
		 													ON members.PeopleId = extras.PeopleId
		 														AND extras.Field = 'PIN'
		 												LEFT JOIN dbo.FamilyCheckinLock AS lock
		 													ON family.FamilyId = lock.FamilyId
		 														AND DATEDIFF( s, lock.Created, GETDATE( ) ) < 60
		 														AND Locked = 1
		 											WHERE REPLACE( family.HomePhone, '-', '' ) LIKE @search
		 													OR REPLACE( members.CellPhone, '-', '' ) LIKE @search
		 													OR REPLACE( members.WorkPhone, '-', '' ) LIKE @search
		 													OR REPLACE( extras.Data, '-', '' ) LIKE @search
		 											GROUP BY family.FamilyId";

			using( SqlCommand cmd = new SqlCommand( qFamilies, db ) ) {
				SqlParameter parameter = new SqlParameter( "search", $"%{search}" );

				cmd.Parameters.Add( parameter );

				SqlDataAdapter adapter = new SqlDataAdapter( cmd );
				adapter.Fill( table );
			}

			foreach( DataRow row in table.Rows ) {
				Family family = new Family();
				family.populate( row );
				family.loadPicture();
				family.loadMembers( db, campus, date );

				families.Add( family );
			}

			return families;
		}

		private void loadMembers( SqlConnection db, int campus, DateTime date )
		{
			members.AddRange( FamilyMember.forFamilyID( db, id, campus, date ) );
		}

		private void loadPicture()
		{
			CmsData.Family family = CmsData.DbUtil.Db.Families.SingleOrDefault( f => f.FamilyId == id );

			if( family == null || family.Picture == null ) return;

			ImageData.Image image = ImageData.DbUtil.Db.Images.SingleOrDefault( i => i.Id == family.Picture.SmallId );

			if( image != null ) {
				picture = Convert.ToBase64String( image.Bits );
			}
		}
	}
}