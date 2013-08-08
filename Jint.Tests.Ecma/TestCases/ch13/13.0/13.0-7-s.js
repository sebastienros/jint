/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-7-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when the code of this FunctionDeclaration is contained in non-strict mode but the call to eval is a direct call in strict mode code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("'use strict'; function _13_0_7_fun() {eval = 42;};");
            _13_0_7_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
