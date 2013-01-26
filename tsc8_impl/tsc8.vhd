library ieee;
use ieee.std_logic_1164.all;
use IEEE.std_logic_arith.all;

library lpm;
use lpm.lpm_components.all;

entity tsc8 is
	port(
		 signal clk,rst: in std_logic;
		 signal rx: out std_logic_vector(5 downto 0);
		 signal ry: out std_logic_vector(4 downto 0);
		 signal rc,drq : out std_logic;
		 signal dak,ro,vsync: in std_logic;
		 signal key1,key2: in std_logic
		 );
end tsc8;

architecture rtl of tsc8 is
    --type state_t is (sFetch,sExec,sVWAIT,sVWAIT_0,sDRAW);
	type reg_t is array(15 downto 0) of unsigned(7 downto 0);
	signal reg: reg_t := (others => X"00" );
	
	signal pc:std_logic_vector(7 downto 0);
	signal idec:std_logic_vector(7 downto 0);
	signal rp:unsigned(7 downto 0);
	signal ram_out:std_logic_vector(7 downto 0);
	
	constant sFetch:unsigned(2 downto 0):="000";
	constant sExec:unsigned(2 downto 0):="001";
	constant sVWAIT:unsigned(2 downto 0):="010";
	constant sVWAIT_0:unsigned(2 downto 0):="011";
	constant sDRAW:unsigned(2 downto 0):="100";
	constant sFetch2:unsigned(2 downto 0):="101";
	
	signal state: unsigned(2 downto 0) := (sFetch);
	
	constant op_mfr0:std_logic_vector(2 downto 0) 	:= "000";
	constant op_mtr0:std_logic_vector(2 downto 0) 	:= "001";
	constant op_add:std_logic_vector(2 downto 0) 	:= "010";
	constant op_neg:std_logic_vector(2 downto 0) 	:= "011";
	constant op_draw:std_logic_vector(2 downto 0) 	:= "100";
	constant op_bgroup:std_logic_vector(2 downto 0) := "101";
	constant op_jz:std_logic_vector(2 downto 0) 	:= "110";
	constant op_jgtz:std_logic_vector(2 downto 0) 	:= "111";

	constant op_b_vsync:std_logic_vector(3 downto 0) 	:= "0000";
	constant op_b_key1:std_logic_vector(3 downto 0) 	:= "0001";
	constant op_b_key2:std_logic_vector(3 downto 0) 	:= "0010";
	constant op_b_jr0:std_logic_vector(3 downto 0) 		:= "0011";	
begin
	eRam: lpm_rom generic map(  LPM_WIDTH =>8,  LPM_WIDTHAD => 8 , LPM_FILE => "cpu_data.mif", LPM_OUTDATA=>"UNREGISTERED")

					 port map(	address => pc, inclock => clk, q =>  ram_out );
        
	Process
	Begin
		Wait until (clk'Event) and (clk='1');
		
		rx <= std_logic_vector(reg(6))(5 downto 0);
		ry <= std_logic_vector(reg(7))(4 downto 0);
		
		if (rst='0') then
			reg <= (others=>X"00");
			drq <= '0';
			rc <= '0';
			state <= sFetch;
			pc <= X"00";
		else
			case state is
				when (sFetch)=>
					state<=sFetch2;
				when (sFetch2)=>
					idec<=ram_out;
					state<=sExec;
					rp<=reg(conv_integer(unsigned(ram_out(3 downto 0))));					
				when (sExec)=>
					state <= sFetch;
					pc <= unsigned(pc) + 1;
					
					if (idec(7) = '0') then
						--LI
						reg(0) <= unsigned(idec);
						reg(0)(7 downto 7) <= "0";
					else
						case idec(6 downto 4) is 
							when (op_mfr0) => 
								reg(conv_integer(unsigned(idec(3 downto 0)))) <= reg(0);
							when (op_mtr0) => 
								reg(0) <= rp;
							when (op_add) =>
								reg(0) <= reg(0) + rp;
							when (op_neg) =>
								reg(0) <= unsigned(NOT std_logic_vector(rp)) + "1";
							when (op_jz) =>
								if (unsigned(rp)=0) then
									pc<=std_logic_vector(reg(0));
								end if;
							when (op_jgtz) =>
								if (signed(rp)>0)  then
									pc<=std_logic_vector(reg(0));
								end if;
							when (op_draw) =>
								rc<=std_logic(rp(0));
								drq <= '1';
								state<=sDRAW;
							when (op_bgroup) =>
								case idec(3 downto 0) is
									when (op_b_vsync) => 
										--wait for vsync ..
										state<=sVWAIT;
									when (op_b_key1)  => 
										if (key1 = '1') then
											reg(0) <= X"01";
										else
											reg(0) <= X"00";
										end if;
									when (op_b_key2)  => 
										if (key2 = '1') then
											reg(0) <= X"01";
										else
											reg(0) <= X"00";
										end if;
									when (op_b_jr0)   =>
										pc<=std_logic_vector(reg(0));
									when others => null;
								end case;
							when others => null;
						end case;
					end if;
				when (sVWAIT) =>
					if (vsync = '1') then
						state<=sVWAIT_0;
					end if;
				when (sVWAIT_0) =>
					if (vsync = '0') then
						state<=sFetch;
					end if;
				when (sDRAW) =>				
					if (dak='1') then
						state<=sFetch;
						drq <= '0';
						if (ro='0') then
							reg(0)<=X"00";
						else
							reg(0)<=X"01";
						end if;
					end if;
				when others => null;
			end case;
		end if;		
	End Process;

end architecture rtl;