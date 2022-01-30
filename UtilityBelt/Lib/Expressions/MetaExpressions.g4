grammar MetaExpressions;

parse               : (expression (';' expression)* ';'?) EOF;

expression          : '(' expression ')'                                                          #parenthesisExp

                    | id=( '$' | '@' | '&' ) expression                                           #getvarAtomExp
                    | expression ('{' (c=':' | (c=':' i2=expression) | i1=expression | (i1=expression c=':' i2=expression) | (i1=expression c=':')) '}')    #getindexAtomExp
                    | (MINUS)? NUMBER                                                             #numericAtomExp
                    | STRING expressionList                                                       #functionCall
                    | <assoc=right> expression '^' expression                                     #powerExp
                    | expression op=( '*' | '/' | '%' ) expression                                #mulDivExp
                    | expression op=( '+' | '-' ) expression                                      #addSubExp
                    | expression '#' expression                                                   #regexExp
                    | expression op=( '>' | '<' | '>=' | '<=' | '==' | '!=' ) expression          #comparisonExp
                    | expression op=( '&&' | '||' ) expression                                    #booleanComparisonExp
                    | BOOL                                                                        #boolAtomExp
                    | STRING                                                                      #stringAtomExp                    
                    | HEXNUMBER                                                                   #hexNumberAtomExp
                    | .                                                                           #catchallAtomExp
                    ;

expressionList      : '[' (expression (',' expression)*)? ']' ;

BOOL                : ([tT][rR][uU][eE] | [fF][aA][lL][sS][eE]) ;
MINUS               : '-';
NUMBER              : (DIGIT* '.'? DIGIT+);
HEXNUMBER           : '0x' [0-9A-Fa-f]+;
STRING              : ( ('`' (BSLASH '`' | ~[`])* '`') | (([@a-zA-Z_'"] | BSLASH .)+ ([@a-zA-Z0-9_'" ]+ | BSLASH .)*) ) ;
WHITESPACE          : [ \t\r\n]+ -> skip;

fragment DIGIT      : [0-9] ;
fragment BSLASH     : '\\' ;