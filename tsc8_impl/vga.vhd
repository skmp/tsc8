library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_arith.all;

entity vga is
	port(
		 signal clk,rst: in std_logic;
		 signal vga_hsync,vga_vsync,vga_r,vga_g,vga_b: out std_logic;
		 
		 signal vpi_h,vpi_v: out unsigned(9 downto 0);
		 signal vpi_active,vpi_vsync:out std_logic;
		 signal vpi_red,vpi_green,vpi_blue: in std_logic
		 );
end vga;

architecture vga_impl of vga is
	--800x525, including sync regions
	constant H_max:unsigned(9 downto 0) := conv_unsigned(799,10);
	constant V_max:unsigned(9 downto 0) := conv_unsigned(524,10);
	signal H_count,V_count: unsigned(9 downto 0);
begin
	
	Process
	Begin
		Wait until (clk'Event) and (clk='1');
		if (rst = '0') then
			H_count <= "0000000000";
			V_count <= "0000000000";
		else
			if (H_count >= H_max) then
				H_count <= "0000000000";
			else
				H_count <= H_count + 1;
			end if;
			
			if (V_count >=V_max) and (H_count>=699) then
				V_count <= "0000000000";
			else 
				if (H_count = 699) then
					V_count <= V_count + 1;
				end if;
			end if;			
		end if;
		
		if (H_count >=659) and (H_count<=755) then
			vga_hsync <= '0';
		else
			vga_hsync <= '1';
		end if;
		
		if (V_count >=493) and (V_count <= 494) then
			vga_vsync <= '0';
		else
			vga_vsync <= '1';
		end if;
		
		if (V_count>=480) then
			vpi_vsync <= '1';
		else
			vpi_vsync <= '0';
		end if;
	
		if (V_count < 480) and (H_count < 640) then
			--DAC
			vga_r <= vpi_red;
			vga_g <= vpi_green;
			vga_b <= vpi_blue;
			--VPI
			vpi_active <= '1';
			vpi_h <= H_count;
			vpi_v <= V_count;
		else
			--DAC
			vga_r <= '0';
			vga_g <= '0';
			vga_b <= '0';
			--VPI
			vpi_active <= '0';
			vpi_h <= "0000000000";
			vpi_v <= "0000000000";
		end if;
	End Process;

end architecture vga_impl;
