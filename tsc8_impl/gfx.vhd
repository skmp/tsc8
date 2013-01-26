library ieee;
use ieee.std_logic_1164.all;
use ieee.std_logic_arith.all;

library lpm;
use lpm.lpm_components.all;

entity gfx is
	port(
		 signal clk,rst: in std_logic;
		 signal rx:in std_logic_vector(5 downto 0);
		 signal ry:in std_logic_vector(4 downto 0);
		 signal rc,drq:in std_logic;
		 signal vpi_h,vpi_v: in unsigned(9 downto 0);
		 signal vpi_red,vpi_green,vpi_blue,dak,ro: out std_logic;
		 signal vpi_vsync: in std_logic;
		 signal vsync_done: out std_logic
		 );
end gfx;

architecture gfx_impl of gfx is
	--64x32 -> 2048 bits
	signal fram_addr,vram_addr:std_logic_vector(10 downto 0) := "00000000000";
	signal vram_data_out,vram_data_in,fram_data_out,fram_data_in:std_logic_vector(0 downto 0);
	signal fram_wen,vram_wen:std_logic := '0';
	
	signal vsync_copy: std_logic;
	
	signal state :unsigned(2 downto 0);
	
	constant sIDLE:unsigned(2 downto 0):="000";
	constant sRead:unsigned(2 downto 0):="001";
	constant sWriten:unsigned(2 downto 0):="010";
	constant sCopySetup:unsigned(2 downto 0):="011";
	constant sCopy:unsigned(2 downto 0):="100";
	constant sCopyEnd:unsigned(2 downto 0):="101";
	
    function calc_vram_adr(rx: std_logic_vector(5 downto 0); ry: std_logic_vector(4 downto 0)) 
		return std_logic_vector is
		variable rv:std_logic_vector(10 downto 0); 
    begin
		rv:=ry(4) & ry(3) & ry(2) & ry(1) & ry(0) & rx(5) & rx(4) & rx(3) & rx(2) & rx(1) & rx(0);
		return rv ;
    end;
    
begin
	vRam_b0: lpm_ram_dq generic map(  LPM_WIDTH => 1,  LPM_WIDTHAD => 11, LPM_FILE => "vram.mif") 
					 port map(	address => vram_addr, we => vram_wen, inclock => clk, outclock => clk, 
								data => vram_data_in, q => vram_data_out );
	fRam_b0: lpm_ram_dq generic map(  LPM_WIDTH => 1,  LPM_WIDTHAD => 11, LPM_FILE => "vram.mif") 
					 port map(	address => fram_addr, we => fram_wen, inclock => clk, outclock => clk, 
								data => fram_data_in, q => fram_data_out );

	Process
		variable tempdata: std_logic;
		variable lx_n: std_logic_vector(5 downto 0);	--63~0, 6 bits
		variable ly: std_logic_vector(4 downto 0);  --31~0, 5 bits
	Begin
		Wait until (clk'Event) and (clk='1');
		--lx:=std_logic_vector(shr(vpi_h-64,X"3"))(5 downto 0);
		lx_n:=std_logic_vector(shr(vpi_h-64+1,X"3"))(5 downto 0);
		ly:=std_logic_vector(shr(vpi_v-112,X"3"))(4 downto 0);
		
		if (vpi_h=1 and vpi_v=0) then
			vsync_done <= '1';
			vsync_copy <= '1';
		else
			vsync_done <= '0';
		end if;
		
		if (rst='0') then
			vram_wen <= '0';
			fram_wen <= '0';
			state  <= sIDLE;
		else
			case state is
				when (sIDLE) =>
					dak <= '0';
					if (vpi_vsync='1' and vsync_copy='1') then
						vsync_copy <= '0' ;
						state<=sCopySetup;
						fram_wen<='0';
						fram_addr<="00000000000";
					elsif (drq='1') then
						state<=sRead;
						fram_addr<=calc_vram_adr(rx,ry);--"00000000";--
						fram_wen<='0';
					end if;
				when (sRead) =>
					state<=sWriten;
					fram_wen<='1';
					fram_data_in<=fram_data_out;
					ro<=fram_data_out(0);
					fram_data_in(0)<=rc;
				when (sWriten) =>
					fram_wen<='0';
					dak<='1';
					if (drq='0') then
						state<=sIDLE;
					end if;
				when (sCopySetup) =>
					state <= sCopy;
					vram_wen<='1';
					vram_addr<="00000000000";
					fram_addr<="00000000000";
					vram_data_in<="0";
				when (sCopy) =>
					if (fram_addr = "11111111111") then
						state <= sCopyEnd;
					end if;
					vram_addr<=fram_addr;
					vram_data_in<=fram_data_out;
					fram_addr <= unsigned(fram_addr) + 1;
				when (sCopyEnd) =>
					state <= sIDLE;
					vram_wen<='0';
				when others =>
					state <= sIDLE;
			end case;

			if (state/=sCopySetup) and (state/=sCopy) and (state/=sCopyEnd) then
				if (vpi_v>=112) and (vpi_v<368) then
					
					--set next address
					vram_addr <= calc_vram_adr(lx_n,ly);
					
					if (vpi_h>=64) and (vpi_h<576) then
						--active display area
						
						tempdata := vram_data_out(0);
						
						vpi_red<=tempdata;
						vpi_green<=tempdata;
						vpi_blue<= tempdata;
					end if;
				end if;
			end if;
		end if;
		
	End Process VIDEO_DISPLAY;

end architecture gfx_impl;