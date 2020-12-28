grammar MetaExpressions;

parse               : (expression (';' expression)* ';'?) EOF;

expression          : '(' expression ')'                                                          #parenthesisExp
                    | (MEMORYVAR | PERSISTENTVAR | GLOBALVAR) expression                          #getvarAtomExp
                    | STRING expressionList                                                           #functionCall
                    | expression (MULTIPLY | DIVIDE) expression                                   #mulDivExp
                    | expression MODULO expression                                                #moduloExp
                    | expression (PLUS | MINUS) expression                                        #addSubExp
                    | <assoc=right> expression POW expression                                     #powerExp
                    | expression REGEXOP expression                                               #regexExp
                    | expression (GT | LT | GTEQTO | LTEQTO | EQTO | NEQTO) expression            #comparisonExp
                    | expression (AND | OR) expression                                            #booleanComparisonExp
                    | BOOL                                                                        #boolAtomExp
                    | STRING                                                                      #stringAtomExp
                    | (MINUS)? NUMBER                                                             #numericAtomExp
                    ;

expressionList      : '[' (expression (',' expression)*)? ']' ;

MEMORYVAR           : '$' ;
PERSISTENTVAR       : '%' ;
GLOBALVAR           : '&' ;

MULTIPLY            : '*' ;
DIVIDE              : '/' ;
MODULO              : '%' ;
POW                 : '^' ;
PLUS                : '+' ;
MINUS               : '-' ;

AND                 : '&&' ;
OR                  : '||' ;

GT                  : '>' ;
LT                  : '<' ;
GTEQTO              : '>=' ;
LTEQTO              : '<=' ;
EQTO                : '==' ;
NEQTO               : '!=' ;

REGEXOP             : '#' ;
BSLASH              : '\\' ;

BOOL                : ([tT][rR][uU][eE] | [fF][aA][lL][sS][eE]) ;
NUMBER              : (DIGIT* '.'? DIGIT+);
STRING              : ( ('`' (BSLASH '`' | ~[`])* '`') | (([a-zA-Z_'"] | BSLASH .)+ ([a-zA-Z0-9_'" ]+ | BSLASH .)*) ) ;
WHITESPACE          : [ \t\r\n]+ -> skip;

fragment DIGIT      : [0-9] ;