library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_arith.all;

entity vga_draw_test is
	port(
		 signal clk: in std_logic;
		 signal vpi_h,vpi_v: in unsigned(9 downto 0);
		 signal vpi_red,vpi_green,vpi_blue: out std_logic
		 );
end vga_draw_test;

architecture vga_draw_impl of vga_draw_test is
begin
	
	Process
	Begin
		Wait until (clk'Event) and (clk='1');
		
		if (vpi_v >30) and (vpi_v < 420) and (vpi_h >0) and (vpi_h < 640) then
			vpi_red <= '1';
		else
			vpi_red <= '0';
		end if;
		
		if (vpi_h > 240) and (vpi_h < 400) and (vpi_v >60) and (vpi_v < 390)  then
			vpi_blue <= '1';
		else
			vpi_blue <= '0';
		end if;
		
		if (vpi_h > 310) and (vpi_h < 330) and (vpi_v > 230) and (vpi_v < 250) then
			vpi_green <= '1';
		else
			vpi_green <= '0';
		end if;
	End Process;

end architecture vga_draw_impl;