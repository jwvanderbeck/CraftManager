﻿using System;
using System.IO;
using System.Collections.Generic;

using UnityEngine;

using KatLib;

namespace CraftManager
{
    public class CraftData
    {
        //**Class Methods/Variables**//


        public static string save_dir = Paths.joined(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder);

        public static List<CraftData> all_craft = new List<CraftData>();  //will hold all the craft loaded from disk
        public static List<CraftData> filtered  = new List<CraftData>();  //will hold the results of search/filtering to be shown in the UI.
        public static Dictionary<string, AvailablePart> game_parts = new Dictionary<string, AvailablePart>();  //populated on first use, name->part lookup for installed parts

        public static List<string> all_tags = new List<string>();

        public static void load_craft(){            
            string[] craft_file_paths;
            craft_file_paths = Directory.GetFiles(save_dir, "*.craft", SearchOption.AllDirectories);

            all_craft.Clear();
            foreach(string path in craft_file_paths){
                all_craft.Add(new CraftData(path));
            }
        }



        public static void filter_craft(){
            filtered = all_craft;    
        }

        public static void filter_craft(Dictionary<string, object> criteria){
            filtered = all_craft;    
            if(criteria.ContainsKey("search")){
                filtered = filtered.FindAll(craft => craft.name.ToLower().Contains(((string)criteria["search"]).ToLower()));
            }
            if(criteria.ContainsKey("type")){
                Dictionary<string, bool> types = (Dictionary<string, bool>) criteria["type"];
                List<string> selected_types = new List<string>();
                foreach(KeyValuePair<string, bool> t in types){
                    if(t.Value){                        
                        selected_types.Add(t.Key=="Subassemblies" ? "Subassembly" : t.Key);
                    }
                }                                   
                filtered = filtered.FindAll(craft => selected_types.Contains(craft.construction_type));
            }
            if(criteria.ContainsKey("tags")){
                List<string> s_tags = (List<string>)criteria["tags"];
                if((bool)criteria["tag_mode_reduce"]){
                    foreach(string tag in s_tags){
                        filtered = filtered.FindAll(craft => craft.tags().Contains(tag));
                    }
                } else{
                    filtered = filtered.FindAll(craft =>{
                        bool sel = false;
                        foreach(string tag in craft.tags()){
                            if(s_tags.Contains(tag)){
                                sel = true;
                            }
                        }
                        return sel;
                    });
                }
            }
            if(criteria.ContainsKey("sort")){
                string sort_by = (string)criteria["sort"];
                filtered.Sort((x,y) => {
                    //{"name", "part_count", "mass", "date_created", "date_updated", "stage_count"};
                    if(sort_by == "name"){
                        return x.name.CompareTo(y.name);
                    }else if(sort_by == "part_count"){
                        return y.part_count.CompareTo(x.part_count);
                    }else if(sort_by == "stage_count"){
                        return y.stage_count.CompareTo(x.stage_count);
                    }else if(sort_by == "mass"){
                        return y.mass["total"].CompareTo(x.mass["total"]);
                    }else if(sort_by == "date_created"){
                        return x.create_time.CompareTo(y.create_time);
                    }else if(sort_by == "date_updated"){
                        return x.last_updated_time.CompareTo(y.last_updated_time);
                    }else{
                        return x.name.CompareTo(y.name);
                    }
                });
                if(criteria.ContainsKey("reverse_sort") && (bool)criteria["reverse_sort"]){
                    filtered.Reverse();
                }
            }
        }
            
        public static void select_craft(CraftData craft){
            foreach(CraftData list_craft in filtered){
                list_craft.selected = list_craft == craft;
            }
        }

        public static CraftData selected_craft(){                    
            return filtered.Find(c => c.selected == true);
        }



        //**Instance Methods/Variables**//


        public string path = "";
        public string save_name = "";
        public string file_checksum;

        public Texture thumbnail;

        public string name = "";
        public string alt_name = null;
        public string description = "";
        public string construction_type = "";
        public string create_time;
        public string last_updated_time;

        public bool missing_parts = false;
        public bool locked_parts = false;
        public bool selected = false;

        public int stage_count = 0;
        public int part_count = 0;
        public Dictionary<string, float> cost = new Dictionary<string, float> {
            {"dry", 0.0f}, {"fuel", 0.0f}, {"total", 0.0f}
        };
        public Dictionary<string, float> mass = new Dictionary<string, float> {
            {"dry", 0.0f}, {"fuel", 0.0f}, {"total", 0.0f}
        };


        public CraftData(string full_path){
            path = full_path;

            read_craft_info_from_file();

            create_time = System.IO.File.GetCreationTime(path).ToBinary().ToString();
            last_updated_time = System.IO.File.GetLastWriteTime(path).ToBinary().ToString();
            file_checksum = Checksum.digest(File.ReadAllText(path));

            save_name = path.Replace(Paths.joined(KSPUtil.ApplicationRootPath, "saves", ""), "").Split('/')[0];
            thumbnail = ShipConstruction.GetThumbnail("/thumbs/" + save_name + "_" + construction_type + "_" + name);
        }


        private void read_craft_info_from_file(){
            ConfigNode data = ConfigNode.Load(path);
            ConfigNode[] parts = data.GetNodes();
            AvailablePart matched_part;

                

            name = Path.GetFileNameWithoutExtension(path);
            alt_name = data.GetValue("ship");
            description = data.GetValue("description");
            construction_type = data.GetValue("type");
            if(!(construction_type == "SPH" || construction_type == "VAB")){
                construction_type = "Subassembly";
            }
            part_count = parts.Length;
            stage_count = 0;
            missing_parts = false;
            locked_parts = false;


            //interim variables used to collect values from GetPartCostsAndMass (defined outside of loop as a garbage reduction measure)
            float dry_mass = 0;
            float fuel_mass = 0;
            float dry_cost = 0;
            float fuel_cost = 0;
            string stage;


            foreach(ConfigNode part in parts){

                //Set the number of stages in the craft
                stage = part.GetValue("istg");
                if(!String.IsNullOrEmpty(stage)){
                    int stage_number = int.Parse(stage);
                    if(stage_number > stage_count){
                        stage_count = stage_number;
                    }
                }

                //locate part in game_parts and read part cost/mass information.
                matched_part = find_part(get_part_name(part));
                if(matched_part != null){
                    ShipConstruction.GetPartCostsAndMass(part, matched_part, out dry_cost, out fuel_cost, out dry_mass, out fuel_mass);
                    cost["dry"] += dry_cost;
                    cost["fuel"] += fuel_cost;
                    mass["dry"] += dry_mass;
                    mass["fuel"] += fuel_mass;
                    if(!ResearchAndDevelopment.PartTechAvailable(matched_part)){
                        locked_parts = true;
                    }

                } else{
                    missing_parts = true;
                }

            }

            stage_count += 1; //this might not be right
            cost["total"] = cost["dry"] + cost["fuel"];
            mass["total"] = mass["dry"] + mass["fuel"];

        }

        //get the part name from a PART config node.
        private string get_part_name(ConfigNode part){
            string part_name = part.GetValue("part");
            if(!String.IsNullOrEmpty(part_name)){
                part_name = part_name.Split('_')[0];
            } else{
                part_name = "";
            }
            return part_name;
        }


        private AvailablePart find_part(string part_name){
            if(game_parts.Count == 0){
                CraftManager.log("caching game parts");
                foreach(AvailablePart part in PartLoader.LoadedPartsList){
                    game_parts.Add(part.name, part);
                }
            }
            if(game_parts.ContainsKey(part_name)){
                return game_parts[part_name];
            }
            return null;
        }

        public List<string> tags(){
            return Tags.tags_for(Tags.craft_reference_key(this));
        }

    }

}

