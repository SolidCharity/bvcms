using System; 
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Data;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Linq.Expressions;
using System.ComponentModel;

namespace CmsData
{
	[Table(Name="dbo.Content")]
	public partial class Content : INotifyPropertyChanging, INotifyPropertyChanged
	{
		private static PropertyChangingEventArgs emptyChangingEventArgs = new PropertyChangingEventArgs(String.Empty);
		
	#region Private Fields
		
		private string _Name;
		
		private string _Title;
		
		private string _Body;
		
		private DateTime? _DateCreated;
		
		private int _Id;
		
		private bool? _TextOnly;
		
		private int _TypeID;
		
		private int _ThumbID;
		
		private int _RoleID;
		
		private int _OwnerID;
		
   		
    	
	#endregion
	
    #region Extensibility Method Definitions
    partial void OnLoaded();
    partial void OnValidate(System.Data.Linq.ChangeAction action);
    partial void OnCreated();
		
		partial void OnNameChanging(string value);
		partial void OnNameChanged();
		
		partial void OnTitleChanging(string value);
		partial void OnTitleChanged();
		
		partial void OnBodyChanging(string value);
		partial void OnBodyChanged();
		
		partial void OnDateCreatedChanging(DateTime? value);
		partial void OnDateCreatedChanged();
		
		partial void OnIdChanging(int value);
		partial void OnIdChanged();
		
		partial void OnTextOnlyChanging(bool? value);
		partial void OnTextOnlyChanged();
		
		partial void OnTypeIDChanging(int value);
		partial void OnTypeIDChanged();
		
		partial void OnThumbIDChanging(int value);
		partial void OnThumbIDChanged();
		
		partial void OnRoleIDChanging(int value);
		partial void OnRoleIDChanged();
		
		partial void OnOwnerIDChanging(int value);
		partial void OnOwnerIDChanged();
		
    #endregion
		public Content()
		{
			
			
			OnCreated();
		}

		
    #region Columns
		
		[Column(Name="Name", UpdateCheck=UpdateCheck.Never, Storage="_Name", DbType="varchar(500) NOT NULL")]
		public string Name
		{
			get { return this._Name; }

			set
			{
				if (this._Name != value)
				{
				
                    this.OnNameChanging(value);
					this.SendPropertyChanging();
					this._Name = value;
					this.SendPropertyChanged("Name");
					this.OnNameChanged();
				}

			}

		}

		
		[Column(Name="Title", UpdateCheck=UpdateCheck.Never, Storage="_Title", DbType="varchar(500)")]
		public string Title
		{
			get { return this._Title; }

			set
			{
				if (this._Title != value)
				{
				
                    this.OnTitleChanging(value);
					this.SendPropertyChanging();
					this._Title = value;
					this.SendPropertyChanged("Title");
					this.OnTitleChanged();
				}

			}

		}

		
		[Column(Name="Body", UpdateCheck=UpdateCheck.Never, Storage="_Body", DbType="varchar")]
		public string Body
		{
			get { return this._Body; }

			set
			{
				if (this._Body != value)
				{
				
                    this.OnBodyChanging(value);
					this.SendPropertyChanging();
					this._Body = value;
					this.SendPropertyChanged("Body");
					this.OnBodyChanged();
				}

			}

		}

		
		[Column(Name="DateCreated", UpdateCheck=UpdateCheck.Never, Storage="_DateCreated", DbType="datetime")]
		public DateTime? DateCreated
		{
			get { return this._DateCreated; }

			set
			{
				if (this._DateCreated != value)
				{
				
                    this.OnDateCreatedChanging(value);
					this.SendPropertyChanging();
					this._DateCreated = value;
					this.SendPropertyChanged("DateCreated");
					this.OnDateCreatedChanged();
				}

			}

		}

		
		[Column(Name="Id", UpdateCheck=UpdateCheck.Never, Storage="_Id", AutoSync=AutoSync.OnInsert, DbType="int NOT NULL IDENTITY", IsPrimaryKey=true, IsDbGenerated=true)]
		public int Id
		{
			get { return this._Id; }

			set
			{
				if (this._Id != value)
				{
				
                    this.OnIdChanging(value);
					this.SendPropertyChanging();
					this._Id = value;
					this.SendPropertyChanged("Id");
					this.OnIdChanged();
				}

			}

		}

		
		[Column(Name="TextOnly", UpdateCheck=UpdateCheck.Never, Storage="_TextOnly", DbType="bit")]
		public bool? TextOnly
		{
			get { return this._TextOnly; }

			set
			{
				if (this._TextOnly != value)
				{
				
                    this.OnTextOnlyChanging(value);
					this.SendPropertyChanging();
					this._TextOnly = value;
					this.SendPropertyChanged("TextOnly");
					this.OnTextOnlyChanged();
				}

			}

		}

		
		[Column(Name="TypeID", UpdateCheck=UpdateCheck.Never, Storage="_TypeID", DbType="int NOT NULL")]
		public int TypeID
		{
			get { return this._TypeID; }

			set
			{
				if (this._TypeID != value)
				{
				
                    this.OnTypeIDChanging(value);
					this.SendPropertyChanging();
					this._TypeID = value;
					this.SendPropertyChanged("TypeID");
					this.OnTypeIDChanged();
				}

			}

		}

		
		[Column(Name="ThumbID", UpdateCheck=UpdateCheck.Never, Storage="_ThumbID", DbType="int NOT NULL")]
		public int ThumbID
		{
			get { return this._ThumbID; }

			set
			{
				if (this._ThumbID != value)
				{
				
                    this.OnThumbIDChanging(value);
					this.SendPropertyChanging();
					this._ThumbID = value;
					this.SendPropertyChanged("ThumbID");
					this.OnThumbIDChanged();
				}

			}

		}

		
		[Column(Name="RoleID", UpdateCheck=UpdateCheck.Never, Storage="_RoleID", DbType="int NOT NULL")]
		public int RoleID
		{
			get { return this._RoleID; }

			set
			{
				if (this._RoleID != value)
				{
				
                    this.OnRoleIDChanging(value);
					this.SendPropertyChanging();
					this._RoleID = value;
					this.SendPropertyChanged("RoleID");
					this.OnRoleIDChanged();
				}

			}

		}

		
		[Column(Name="OwnerID", UpdateCheck=UpdateCheck.Never, Storage="_OwnerID", DbType="int NOT NULL")]
		public int OwnerID
		{
			get { return this._OwnerID; }

			set
			{
				if (this._OwnerID != value)
				{
				
                    this.OnOwnerIDChanging(value);
					this.SendPropertyChanging();
					this._OwnerID = value;
					this.SendPropertyChanged("OwnerID");
					this.OnOwnerIDChanged();
				}

			}

		}

		
    #endregion
        
    #region Foreign Key Tables
   		
	#endregion
	
	#region Foreign Keys
    	
	#endregion
	
		public event PropertyChangingEventHandler PropertyChanging;
		protected virtual void SendPropertyChanging()
		{
			if ((this.PropertyChanging != null))
				this.PropertyChanging(this, emptyChangingEventArgs);
		}

		public event PropertyChangedEventHandler PropertyChanged;
		protected virtual void SendPropertyChanged(String propertyName)
		{
			if ((this.PropertyChanged != null))
				this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}

   		
	}

}

