/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-13-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when the function body of a Function constructor begins with a Strict Directive
 * @onlyStrict
 */


function testcase() {
       
        try {
            eval("var _13_0_13_fun = new Function(\" \", \"'use strict'; eval = 42;\"); _13_0_13_fun();");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
