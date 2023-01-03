using Decal.Adapter.Wrappers;
using System;
using System.Collections.Generic;
using System.Data;

namespace UtilityBelt.Lib.ItemInfoHelper {
    class BestValuesDatatable {

        public static void Main() {
            DataTable table = GetTable();
        }

        public static DataTable GetTable() {
            DataTable table = new DataTable();
            table.Columns.Add("Skill", typeof(double));
            table.Columns.Add("Mastery", typeof(double));
            table.Columns.Add("MultiStrike", typeof(double));
            table.Columns.Add("WieldReq", typeof(double));
            table.Columns.Add("MaxDmg", typeof(double));
            table.Columns.Add("MaxVar", typeof(double));
            table.Columns.Add("MaxDmgMod", typeof(double));
            table.Columns.Add("MaxElementalDmgBonus", typeof(double));
            table.Columns.Add("MaxElementalDmgVsMonsters", typeof(double));

            #region heavyweaponry http://acpedia.org/wiki/Heavy_Weaponry
            table.Rows.Add(44, 3, 0,   0, 26, .90); //axe
            table.Rows.Add(44, 3, 0, 250, 33, .90); //axe
            table.Rows.Add(44, 3, 0, 300, 40, .90); //axe
            table.Rows.Add(44, 3, 0, 325, 47, .90); //axe
            table.Rows.Add(44, 3, 0, 350, 54, .90); //axe
            table.Rows.Add(44, 3, 0, 370, 61, .90); //axe
            table.Rows.Add(44, 3, 0, 400, 69, .90); //axe
            table.Rows.Add(44, 3, 0, 420, 71, .90); //axe
            table.Rows.Add(44, 3, 0, 430, 74, .90); //axe

            table.Rows.Add(44, 6, 0,   0, 24, .47); //dagger
            table.Rows.Add(44, 6, 0, 250, 31, .47); //dagger
            table.Rows.Add(44, 6, 0, 300, 38, .47); //dagger
            table.Rows.Add(44, 6, 0, 325, 45, .47); //dagger
            table.Rows.Add(44, 6, 0, 350, 51, .47); //dagger
            table.Rows.Add(44, 6, 0, 370, 58, .47); //dagger
            table.Rows.Add(44, 6, 0, 400, 65, .47); //dagger
            table.Rows.Add(44, 6, 0, 420, 68, .47); //dagger
            table.Rows.Add(44, 6, 0, 430, 71, .47); //dagger

            table.Rows.Add(44, 6, 1,   0, 13, .40); //msdagger
            table.Rows.Add(44, 6, 1, 250, 16, .40); //msdagger
            table.Rows.Add(44, 6, 1, 300, 20, .40); //msdagger
            table.Rows.Add(44, 6, 1, 325, 23, .40); //msdagger
            table.Rows.Add(44, 6, 1, 350, 26, .40); //msdagger
            table.Rows.Add(44, 6, 1, 370, 30, .40); //msdagger
            table.Rows.Add(44, 6, 1, 400, 33, .40); //msdagger
            table.Rows.Add(44, 6, 1, 420, 36, .40); //msdagger
            table.Rows.Add(44, 6, 1, 430, 38, .40); //msdagger

            table.Rows.Add(44, 4, 0,   0, 22, .30); //mace
            table.Rows.Add(44, 4, 0, 250, 29, .30); //mace
            table.Rows.Add(44, 4, 0, 300, 36, .30); //mace
            table.Rows.Add(44, 4, 0, 325, 43, .30); //mace
            table.Rows.Add(44, 4, 0, 350, 49, .30); //mace
            table.Rows.Add(44, 4, 0, 370, 56, .30); //mace
            table.Rows.Add(44, 4, 0, 400, 63, .30); //mace
            table.Rows.Add(44, 4, 0, 420, 66, .30); //mace
            table.Rows.Add(44, 4, 0, 430, 69, .30); //mace

            table.Rows.Add(44, 5, 0,   0, 25, .59); //spear
            table.Rows.Add(44, 5, 0, 250, 32, .59); //spear
            table.Rows.Add(44, 5, 0, 300, 39, .59); //spear
            table.Rows.Add(44, 5, 0, 325, 46, .59); //spear
            table.Rows.Add(44, 5, 0, 350, 58, .59); //spear
            table.Rows.Add(44, 5, 0, 370, 59, .59); //spear
            table.Rows.Add(44, 5, 0, 400, 66, .59); //spear
            table.Rows.Add(44, 5, 0, 420, 69, .59); //spear
            table.Rows.Add(44, 5, 0, 430, 72, .59); //spear

            table.Rows.Add(44, 2, 0,   0, 24, .47); //sword
            table.Rows.Add(44, 2, 0, 250, 31, .47); //sword
            table.Rows.Add(44, 2, 0, 300, 38, .47); //sword
            table.Rows.Add(44, 2, 0, 325, 45, .47); //sword
            table.Rows.Add(44, 2, 0, 350, 51, .47); //sword
            table.Rows.Add(44, 2, 0, 370, 58, .47); //sword
            table.Rows.Add(44, 2, 0, 400, 65, .47); //sword
            table.Rows.Add(44, 2, 0, 420, 68, .47); //sword
            table.Rows.Add(44, 2, 0, 430, 71, .47); //sword

            table.Rows.Add(44, 2, 1,   0, 13, .40); //mssword
            table.Rows.Add(44, 2, 1, 250, 16, .40); //mssword
            table.Rows.Add(44, 2, 1, 300, 20, .40); //mssword
            table.Rows.Add(44, 2, 1, 325, 23, .40); //mssword
            table.Rows.Add(44, 2, 1, 350, 26, .40); //mssword
            table.Rows.Add(44, 2, 1, 370, 30, .40); //mssword
            table.Rows.Add(44, 2, 1, 400, 33, .40); //mssword
            table.Rows.Add(44, 2, 1, 420, 36, .40); //mssword
            table.Rows.Add(44, 2, 1, 430, 38, .40); //mssword

            table.Rows.Add(44, 7, 0,   0, 23, .38); //staff
            table.Rows.Add(44, 7, 0, 250, 30, .38); //staff
            table.Rows.Add(44, 7, 0, 300, 36, .38); //staff
            table.Rows.Add(44, 7, 0, 325, 43, .38); //staff
            table.Rows.Add(44, 7, 0, 350, 50, .38); //staff
            table.Rows.Add(44, 7, 0, 370, 56, .38); //staff
            table.Rows.Add(44, 7, 0, 400, 63, .38); //staff
            table.Rows.Add(44, 7, 0, 420, 66, .38); //staff
            table.Rows.Add(44, 7, 0, 430, 70, .38); //staff

            table.Rows.Add(44, 1, 0,   0, 20, .44); //ua
            table.Rows.Add(44, 1, 0, 250, 26, .44); //ua
            table.Rows.Add(44, 1, 0, 300, 31, .44); //ua
            table.Rows.Add(44, 1, 0, 325, 37, .44); //ua
            table.Rows.Add(44, 1, 0, 350, 43, .44); //ua
            table.Rows.Add(44, 1, 0, 370, 48, .44); //ua
            table.Rows.Add(44, 1, 0, 400, 54, .44); //ua
            table.Rows.Add(44, 1, 0, 420, 56, .44); //ua
            table.Rows.Add(44, 1, 0, 430, 59, .44); //ua
            #endregion

            #region light weaponry http://acpedia.org/wiki/Light_Weaponry
            table.Rows.Add(45, 3, 0,   0, 22, .80); //axe
            table.Rows.Add(45, 3, 0, 250, 28, .80); //axe
            table.Rows.Add(45, 3, 0, 300, 33, .80); //axe
            table.Rows.Add(45, 3, 0, 325, 39, .80); //axe
            table.Rows.Add(45, 3, 0, 350, 44, .80); //axe
            table.Rows.Add(45, 3, 0, 370, 50, .80); //axe
            table.Rows.Add(45, 3, 0, 400, 55, .80); //axe
            table.Rows.Add(45, 3, 0, 420, 57, .80); //axe
            table.Rows.Add(45, 3, 0, 430, 61, .80); //axe

            table.Rows.Add(45, 6, 0,   0, 18, .42); //dagger
            table.Rows.Add(45, 6, 0, 250, 24, .42); //dagger
            table.Rows.Add(45, 6, 0, 300, 29, .42); //dagger
            table.Rows.Add(45, 6, 0, 325, 35, .42); //dagger
            table.Rows.Add(45, 6, 0, 350, 40, .42); //dagger
            table.Rows.Add(45, 6, 0, 370, 46, .42); //dagger
            table.Rows.Add(45, 6, 0, 400, 51, .42); //dagger
            table.Rows.Add(45, 6, 0, 420, 54, .42); //dagger
            table.Rows.Add(45, 6, 0, 430, 58, .42); //dagger

            table.Rows.Add(45, 6, 1,   0,  7, .24); //msdagger
            table.Rows.Add(45, 6, 1, 250, 11, .24); //msdagger
            table.Rows.Add(45, 6, 1, 300, 13, .24); //msdagger
            table.Rows.Add(45, 6, 1, 325, 16, .24); //msdagger
            table.Rows.Add(45, 6, 1, 350, 18, .24); //msdagger
            table.Rows.Add(45, 6, 1, 370, 21, .24); //msdagger
            table.Rows.Add(45, 6, 1, 400, 24, .24); //msdagger
            table.Rows.Add(45, 6, 1, 420, 27, .24); //msdagger
            table.Rows.Add(45, 6, 1, 430, 28, .24); //msdagger

            table.Rows.Add(45, 4, 0,   0, 19, .23); //mace
            table.Rows.Add(45, 4, 0, 250, 24, .23); //mace
            table.Rows.Add(45, 4, 0, 300, 29, .23); //mace
            table.Rows.Add(45, 4, 0, 325, 35, .23); //mace
            table.Rows.Add(45, 4, 0, 350, 40, .23); //mace
            table.Rows.Add(45, 4, 0, 370, 45, .23); //mace
            table.Rows.Add(45, 4, 0, 400, 50, .23); //mace
            table.Rows.Add(45, 4, 0, 420, 53, .23); //mace
            table.Rows.Add(45, 4, 0, 430, 57, .23); //mace

            table.Rows.Add(45, 5, 0, 0,   21, .65); //spear
            table.Rows.Add(45, 5, 0, 250, 26, .65); //spear
            table.Rows.Add(45, 5, 0, 300, 32, .65); //spear
            table.Rows.Add(45, 5, 0, 325, 37, .65); //spear
            table.Rows.Add(45, 5, 0, 350, 42, .65); //spear
            table.Rows.Add(45, 5, 0, 370, 48, .65); //spear
            table.Rows.Add(45, 5, 0, 400, 53, .65); //spear
            table.Rows.Add(45, 5, 0, 420, 57, .65); //spear
            table.Rows.Add(45, 5, 0, 430, 60, .65); //spear

            table.Rows.Add(45, 2, 0,   0, 20, .42); //sword
            table.Rows.Add(45, 2, 0, 250, 25, .42); //sword
            table.Rows.Add(45, 2, 0, 300, 31, .42); //sword
            table.Rows.Add(45, 2, 0, 325, 36, .42); //sword
            table.Rows.Add(45, 2, 0, 350, 41, .42); //sword
            table.Rows.Add(45, 2, 0, 370, 47, .42); //sword
            table.Rows.Add(45, 2, 0, 400, 52, .42); //sword
            table.Rows.Add(45, 2, 0, 420, 55, .42); //sword
            table.Rows.Add(45, 2, 0, 430, 58, .42); //sword

            table.Rows.Add(45, 2, 1,   0,  7, .24); //mssword
            table.Rows.Add(45, 2, 1, 250, 10, .24); //mssword
            table.Rows.Add(45, 2, 1, 300, 13, .24); //mssword
            table.Rows.Add(45, 2, 1, 325, 16, .24); //mssword
            table.Rows.Add(45, 2, 1, 350, 18, .24); //mssword
            table.Rows.Add(45, 2, 1, 370, 21, .24); //mssword
            table.Rows.Add(45, 2, 1, 400, 24, .24); //mssword
            table.Rows.Add(45, 2, 1, 420, 25, .24); //mssword
            table.Rows.Add(45, 2, 1, 430, 28, .24); //mssword

            table.Rows.Add(45, 7, 0,   0, 19, .325); //staff
            table.Rows.Add(45, 7, 0, 250, 24, .325); //staff
            table.Rows.Add(45, 7, 0, 300, 30, .325); //staff
            table.Rows.Add(45, 7, 0, 325, 35, .325); //staff
            table.Rows.Add(45, 7, 0, 350, 40, .325); //staff
            table.Rows.Add(45, 7, 0, 370, 46, .325); //staff
            table.Rows.Add(45, 7, 0, 400, 51, .325); //staff
            table.Rows.Add(45, 7, 0, 420, 54, .325); //staff
            table.Rows.Add(45, 7, 0, 430, 57, .325); //staff

            table.Rows.Add(45, 1, 0,   0, 17, .43); //ua
            table.Rows.Add(45, 1, 0, 250, 23, .43); //ua
            table.Rows.Add(45, 1, 0, 300, 27, .43); //ua
            table.Rows.Add(45, 1, 0, 325, 31, .43); //ua
            table.Rows.Add(45, 1, 0, 350, 35, .43); //ua
            table.Rows.Add(45, 1, 0, 370, 40, .43); //ua
            table.Rows.Add(45, 1, 0, 400, 44, .43); //ua
            table.Rows.Add(45, 1, 0, 420, 46, .43); //ua
            table.Rows.Add(45, 1, 0, 430, 48, .43); //ua
            #endregion

            #region finesse weaponry http://acpedia.org/wiki/Finesse_Weaponry
            table.Rows.Add(46, 3, 0,   0, 22, .80); //axe
            table.Rows.Add(46, 3, 0, 250, 28, .80); //axe
            table.Rows.Add(46, 3, 0, 300, 33, .80); //axe
            table.Rows.Add(46, 3, 0, 325, 39, .80); //axe
            table.Rows.Add(46, 3, 0, 350, 44, .80); //axe
            table.Rows.Add(46, 3, 0, 370, 50, .80); //axe
            table.Rows.Add(46, 3, 0, 400, 55, .80); //axe
            table.Rows.Add(46, 3, 0, 420, 57, .80); //axe
            table.Rows.Add(46, 3, 0, 430, 61, .80); //axe
                            
            table.Rows.Add(46, 6, 0,   0, 18, .42); //dagger
            table.Rows.Add(46, 6, 0, 250, 24, .42); //dagger
            table.Rows.Add(46, 6, 0, 300, 29, .42); //dagger
            table.Rows.Add(46, 6, 0, 325, 35, .42); //dagger
            table.Rows.Add(46, 6, 0, 350, 40, .42); //dagger
            table.Rows.Add(46, 6, 0, 370, 46, .42); //dagger
            table.Rows.Add(46, 6, 0, 400, 51, .42); //dagger
            table.Rows.Add(46, 6, 0, 420, 54, .42); //dagger
            table.Rows.Add(46, 6, 0, 430, 58, .42); //dagger
                            
            table.Rows.Add(46, 6, 1,   0,  7, .24); //msdagger
            table.Rows.Add(46, 6, 1, 250, 11, .24); //msdagger
            table.Rows.Add(46, 6, 1, 300, 13, .24); //msdagger
            table.Rows.Add(46, 6, 1, 325, 16, .24); //msdagger
            table.Rows.Add(46, 6, 1, 350, 18, .24); //msdagger
            table.Rows.Add(46, 6, 1, 370, 21, .24); //msdagger
            table.Rows.Add(46, 6, 1, 400, 24, .24); //msdagger
            table.Rows.Add(46, 6, 1, 420, 27, .24); //msdagger
            table.Rows.Add(46, 6, 1, 430, 28, .24); //msdagger
                            
            table.Rows.Add(46, 4, 0,   0, 19, .23); //mace
            table.Rows.Add(46, 4, 0, 250, 24, .23); //mace
            table.Rows.Add(46, 4, 0, 300, 29, .23); //mace
            table.Rows.Add(46, 4, 0, 325, 35, .23); //mace
            table.Rows.Add(46, 4, 0, 350, 40, .23); //mace
            table.Rows.Add(46, 4, 0, 370, 45, .23); //mace
            table.Rows.Add(46, 4, 0, 400, 50, .23); //mace
            table.Rows.Add(46, 4, 0, 420, 53, .23); //mace
            table.Rows.Add(46, 4, 0, 430, 57, .23); //mace
                            
            table.Rows.Add(46, 5, 0,   0, 21, .65); //spear
            table.Rows.Add(46, 5, 0, 250, 26, .65); //spear
            table.Rows.Add(46, 5, 0, 300, 32, .65); //spear
            table.Rows.Add(46, 5, 0, 325, 37, .65); //spear
            table.Rows.Add(46, 5, 0, 350, 42, .65); //spear
            table.Rows.Add(46, 5, 0, 370, 48, .65); //spear
            table.Rows.Add(46, 5, 0, 400, 53, .65); //spear
            table.Rows.Add(46, 5, 0, 420, 57, .65); //spear
            table.Rows.Add(46, 5, 0, 430, 60, .65); //spear
                            
            table.Rows.Add(46, 2, 0,   0, 20, .42); //sword
            table.Rows.Add(46, 2, 0, 250, 25, .42); //sword
            table.Rows.Add(46, 2, 0, 300, 31, .42); //sword
            table.Rows.Add(46, 2, 0, 325, 36, .42); //sword
            table.Rows.Add(46, 2, 0, 350, 41, .42); //sword
            table.Rows.Add(46, 2, 0, 370, 47, .42); //sword
            table.Rows.Add(46, 2, 0, 400, 52, .42); //sword
            table.Rows.Add(46, 2, 0, 420, 55, .42); //sword
            table.Rows.Add(46, 2, 0, 430, 58, .42); //sword
                            
            table.Rows.Add(46, 2, 1,   0, 7, .24); //mssword
            table.Rows.Add(46, 2, 1, 250, 10, .24); //mssword
            table.Rows.Add(46, 2, 1, 300, 13, .24); //mssword
            table.Rows.Add(46, 2, 1, 325, 16, .24); //mssword
            table.Rows.Add(46, 2, 1, 350, 18, .24); //mssword
            table.Rows.Add(46, 2, 1, 370, 21, .24); //mssword
            table.Rows.Add(46, 2, 1, 400, 24, .24); //mssword
            table.Rows.Add(46, 2, 1, 420, 25, .24); //mssword
            table.Rows.Add(46, 2, 1, 430, 28, .24); //mssword
                            
            table.Rows.Add(46, 7, 0,   0, 19, .325); //staff
            table.Rows.Add(46, 7, 0, 250, 24, .325); //staff
            table.Rows.Add(46, 7, 0, 300, 30, .325); //staff
            table.Rows.Add(46, 7, 0, 325, 35, .325); //staff
            table.Rows.Add(46, 7, 0, 350, 40, .325); //staff
            table.Rows.Add(46, 7, 0, 370, 46, .325); //staff
            table.Rows.Add(46, 7, 0, 400, 51, .325); //staff
            table.Rows.Add(46, 7, 0, 420, 54, .325); //staff
            table.Rows.Add(46, 7, 0, 430, 57, .325); //staff
                            
            table.Rows.Add(46, 1, 0,   0, 17, .43); //ua
            table.Rows.Add(46, 1, 0, 250, 23, .43); //ua
            table.Rows.Add(46, 1, 0, 300, 27, .43); //ua
            table.Rows.Add(46, 1, 0, 325, 31, .43); //ua
            table.Rows.Add(46, 1, 0, 350, 35, .43); //ua
            table.Rows.Add(46, 1, 0, 370, 40, .43); //ua
            table.Rows.Add(46, 1, 0, 400, 44, .43); //ua
            table.Rows.Add(46, 1, 0, 420, 46, .43); //ua
            table.Rows.Add(46, 1, 0, 430, 48, .43); //ua
            #endregion

            #region twohanded http://acpedia.org/wiki/Two_Handed_Weaponry
            table.Rows.Add(41, 11, 1,   0, 13, .30); //cleaver
            table.Rows.Add(41, 11, 1, 250, 17, .30); //cleaver
            table.Rows.Add(41, 11, 1, 300, 22, .30); //cleaver
            table.Rows.Add(41, 11, 1, 325, 26, .30); //cleaver
            table.Rows.Add(41, 11, 1, 350, 30, .30); //cleaver
            table.Rows.Add(41, 11, 1, 370, 35, .30); //cleaver
            table.Rows.Add(41, 11, 1, 400, 39, .30); //cleaver
            table.Rows.Add(41, 11, 1, 420, 42, .30); //cleaver
            table.Rows.Add(41, 11, 1, 430, 45, .30); //cleaver
                           
            table.Rows.Add(41, 11, 0,   0, 14, .35); //spear
            table.Rows.Add(41, 11, 0, 250, 19, .35); //spear
            table.Rows.Add(41, 11, 0, 300, 24, .35); //spear
            table.Rows.Add(41, 11, 0, 325, 29, .35); //spear
            table.Rows.Add(41, 11, 0, 350, 33, .35); //spear
            table.Rows.Add(41, 11, 0, 370, 37, .35); //spear
            table.Rows.Add(41, 11, 0, 400, 42, .35); //spear
            table.Rows.Add(41, 11, 0, 420, 45, .35); //spear
            table.Rows.Add(41, 11, 0, 430, 48, .35); //spear
            #endregion


            #region missile http://acpedia.org/wiki/Missile_Weaponry/Bows
            table.Rows.Add(47, 8, 0, 0, 0, 0, 110, 0); //bow
            table.Rows.Add(47, 8, 0, 250, 0, 0, 120, 0); //bow
            table.Rows.Add(47, 8, 0, 270, 0, 0, 130, 0); //bow
            table.Rows.Add(47, 8, 0, 290, 0, 0, 140, 0); //bow
            table.Rows.Add(47, 8, 0, 315, 0, 0, 140, 5); //bow
            table.Rows.Add(47, 8, 0, 335, 0, 0, 140, 9); //bow
            table.Rows.Add(47, 8, 0, 360, 0, 0, 140, 16); //bow
            table.Rows.Add(47, 8, 0, 375, 0, 0, 140, 19); //bow
            table.Rows.Add(47, 8, 0, 385, 0, 0, 140, 22); //bow

            table.Rows.Add(47, 9, 0, 0, 0, 0, 140, 0); //xbow
            table.Rows.Add(47, 9, 0, 250, 0, 0, 150, 0); //xbow
            table.Rows.Add(47, 9, 0, 270, 0, 0, 155, 0); //xbow
            table.Rows.Add(47, 9, 0, 290, 0, 0, 165, 0); //xbow
            table.Rows.Add(47, 9, 0, 315, 0, 0, 165, 5); //xbow
            table.Rows.Add(47, 9, 0, 335, 0, 0, 165, 9); //xbow
            table.Rows.Add(47, 9, 0, 360, 0, 0, 165, 16); //xbow
            table.Rows.Add(47, 9, 0, 375, 0, 0, 165, 19); //xbow
            table.Rows.Add(47, 9, 0, 385, 0, 0, 165, 22); //xbow

            table.Rows.Add(47, 10, 0, 0, 0, 0, 130, 0); //thrown
            table.Rows.Add(47, 10, 0, 250, 0, 0, 140, 0); //thrown
            table.Rows.Add(47, 10, 0, 270, 0, 0, 150, 0); //thrown
            table.Rows.Add(47, 10, 0, 290, 0, 0, 160, 0); //thrown
            table.Rows.Add(47, 10, 0, 315, 0, 0, 160, 5); //thrown
            table.Rows.Add(47, 10, 0, 335, 0, 0, 160, 9); //thrown
            table.Rows.Add(47, 10, 0, 360, 0, 0, 160, 16); //thrown
            table.Rows.Add(47, 10, 0, 375, 0, 0, 160, 19); //thrown
            table.Rows.Add(47, 10, 0, 385, 0, 0, 160, 22); //thrown
            #endregion

            #region wands http://acpedia.org/wiki/Magic_Casters
            table.Rows.Add(34, 0, 0, 290, 0, 0, 0, 0, 1.03); //war
            table.Rows.Add(34, 0, 0, 315, 0, 0, 0, 0, 1.06); //war
            table.Rows.Add(34, 0, 0, 335, 0, 0, 0, 0, 1.09); //war
            table.Rows.Add(34, 0, 0, 360, 0, 0, 0, 0, 1.13); //war
            table.Rows.Add(34, 0, 0, 375, 0, 0, 0, 0, 1.16); //war
            table.Rows.Add(34, 0, 0, 385, 0, 0, 0, 0, 1.18); //war

            table.Rows.Add(43, 0, 0, 290, 0, 0, 0, 0, 1.03); //void
            table.Rows.Add(43, 0, 0, 315, 0, 0, 0, 0, 1.06); //void
            table.Rows.Add(43, 0, 0, 335, 0, 0, 0, 0, 1.09); //void
            table.Rows.Add(43, 0, 0, 360, 0, 0, 0, 0, 1.13); //void
            table.Rows.Add(43, 0, 0, 375, 0, 0, 0, 0, 1.16); //void
            table.Rows.Add(43, 0, 0, 385, 0, 0, 0, 0, 1.18); //void
            table.Rows.Add(43, 12, 0, 385, 0, 0, 0, 0, 1.18); //void vr wand - not sure why this is different

            #endregion
            return table;
        }


    }
}
