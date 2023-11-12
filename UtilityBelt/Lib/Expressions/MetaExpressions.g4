grammar MetaExpressions;

parse               : (expression (';' expression)* ';'?) EOF;

expression          

                    : '(' functionArgs ')' '=>' '{' ( functionBody | ) '}'                        #inlineFunction
                    | STRING '[' expressionList ']'                                               #functionCall
                    | id=( '$' | '@' | '&' ) expression                                           #getvarAtomExp
                    | expression '(' ( userFunctionCallArgs ) ')'                                 #userFunctionCallExp 
                    | '(' expression ')'                                                          #parenthesisExp
                    | expression ('{' (c=':' | (c=':' i2=expression) | i1=expression | (i1=expression c=':' i2=expression) | (i1=expression c=':')) '}')    #getindexAtomExp
                    | (MINUS)? NUMBER                                                             #numericAtomExp
                    | '~' expression                                                              #bitwiseComplementOp
                    | expression op=( '>>' | '<<' ) expression                                    #bitshiftOps
                    | expression op=( '&' | '^' | '|' ) expression                                #bitwiseOps
                    | <assoc=right> expression '^' expression                                     #powerExp
                    | expression op=( '*' | '/' | '%' ) expression                                #mulDivExp
                    | expression op=( '+' | '-' ) expression                                      #addSubExp
                    | expression '#' expression                                                   #regexExp
                    | id=( '$' | '@' | '&' ) expression '=' expression                            #setVarExp
                    | expression op=( '>' | '<' | '>=' | '<=' | '==' | '!=' ) expression          #comparisonExp
                    | expression op=( '&&' | '||' ) expression                                    #booleanComparisonExp
                    | BOOL                                                                        #boolAtomExp
                    | STRING                                                                      #stringAtomExp                    
                    | HEXNUMBER                                                                   #hexNumberAtomExp
                    | .                                                                           #catchallAtomExp
                    |                                                                             #emptyExp
                    ;
                    
expressionList      : expression (',' expression)* ;
functionBody        : expression (';' expression)* ';'? ;
userFunctionCallArgs: expression (',' expression)* ;

functionArgs        : (STRING (',' STRING)*)? ;

BOOL                : ([tT][rR][uU][eE] | [fF][aA][lL][sS][eE]) ;
MINUS               : '-';
NUMBER              : (DIGIT* '.'? DIGIT+);
HEXNUMBER           : '0x' [0-9A-Fa-f]+;
STRING              : ( ('`' (BSLASH '`' | ~[`])* '`') | (([a-zA-Z_'"] | BSLASH .)+ ([a-zA-Z0-9_'" ]+ | BSLASH .)*) {if (!string.IsNullOrEmpty(_localctx?.GetText())) { this._text = _localctx.GetText().Trim(); } } ) ;
WHITESPACE          : [ \t\r\n]+ -> skip;

fragment DIGIT      : [0-9] ;
fragment BSLASH     : '\\' ;