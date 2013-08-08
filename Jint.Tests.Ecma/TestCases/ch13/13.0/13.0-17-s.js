/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-17-s.js
 * @description Strict Mode - SourceElements is not evaluated as strict mode code when a Function constructor is contained in strict mode code within eval code
 * @onlyStrict
 */


function testcase() {

        eval("'use strict'; var _13_0_17_fun = new Function('eval = 42;'); _13_0_17_fun();");
        return true;
    }
runTestCase(testcase);
