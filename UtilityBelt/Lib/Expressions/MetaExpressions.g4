grammar MetaExpressions;

parse               : (expression | (expression ';')+) EOF;

expression          : '(' expression ')'                                                          #parenthesisExp
                    | expression (ASTERISK|SLASH) expression                                      #mulDivExp
                    | expression MODULO expression                                                #moduloExp
                    | expression (PLUS|MINUS) expression                                          #addSubExp
                    | <assoc=right> expression POW expression                                     #powerExp
					| expression REGEXOP expression				    					          #regexExp
					| expression (GT | LT | GTEQTO | LTEQTO | EQTO | NEQTO) expression            #comparisonExp
					| expression (AND | OR) expression                                            #booleanComparisonExp
					| ID expressionList ']'                                                       #functionCall
					| BOOL       								                                  #boolAtomExp
                    | NUMBER                                                                      #numericAtomExp
                    | STRING                                                                      #stringAtomExp
                    ;

expressionList      : (expression (',' expression)*)? ;
                   

ASTERISK            : '*' ;
SLASH               : '/' ;
BSLASH              : '\\' ;
MODULO              : '%' ;
POW                 : '^' ;
PLUS                : '+' ;
MINUS               : '-' ;
AND                 : '&&' ;
OR					: '||' ;
GT                  : '>' ;
LT                  : '<' ;
GTEQTO              : '>=' ;
LTEQTO              : '<=' ;
EQTO                : '==' ;
NEQTO               : '!=' ;
REGEXOP             : '#' ;

ID                  : [a-zA-Z_] [a-zA-Z0-9_]+ '[' ;
BOOL                : (T R U E | F A L S E) ;
NUMBER              : DIGIT+ ('.' DIGIT+)? ;
STRING              : ( [`] ~[`]* [`] | ([a-zA-Z_'"] | BSLASH .) ([a-zA-Z_'" ] | BSLASH .)* ) ;
WHITESPACE          : [ \t\r\n]+ -> skip;

fragment DIGIT      : [0-9] ;
fragment T          : 'T'|'t' ;
fragment R          : 'R'|'r' ;
fragment U          : 'U'|'u' ;
fragment E          : 'E'|'e' ;
fragment F          : 'F'|'f' ;
fragment A          : 'A'|'a' ;
fragment L          : 'L'|'l' ;
fragment S          : 'S'|'s' ;