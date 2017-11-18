﻿using System;
using System.IO;
//using System.Linq;
using System.Collections.Generic;

using UnityEngine;

using KatLib;

namespace CraftManager
{
    public class CraftData
    {
        public static string save_dir = Paths.joined(KSPUtil.ApplicationRootPath, "saves", HighLogic.SaveFolder);

        public static List<CraftData> all_craft = new List<CraftData>();
        public static List<CraftData> filtered  = new List<CraftData>();
        public static Dictionary<string, AvailablePart> game_parts = new Dictionary<string, AvailablePart>();


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

        public static void select_craft(CraftData craft){
            foreach(CraftData list_craft in filtered){
                list_craft.selected = list_craft == craft;
            }
        }

        public static CraftData selected_craft(){                    
            return filtered.Find(c => c.selected == true);
        }




        public string path = "";
        public string name = "";
        public string description = "";
        public string construction_type = "";

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
            set_info_from_craft_file();
        }

        private void set_info_from_craft_file(){
            ConfigNode data = ConfigNode.Load(path);
            ConfigNode[] parts = data.GetNodes();
            AvailablePart matched_part;

            name = Path.GetFileNameWithoutExtension(path);
            description = data.GetValue("description");
            construction_type = data.GetValue("type");
            if(construction_type != "SPH" || construction_type != "VAB"){
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

            cost["total"] = cost["dry"] + cost["fuel"];
            mass["total"] = mass["dry"] + mass["fuel"];

        }

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

    }

}
