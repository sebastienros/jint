/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.1/11.1.5/11.1.5-3-s.js
 * @description Strict Mode - SyntaxError is thrown when  'evals'  occurs as the Identifier in a PropertySetParameterList of a PropertyAssignment  if its FunctionBody is strict code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("var obj = {set _11_1_5_3_fun(eval) { \"use strict\"; }};");
            return false;
        } catch (e) {
            return (e instanceof SyntaxError);
        }
    }
runTestCase(testcase);
