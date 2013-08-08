/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-15-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when a FunctionDeclaration is contained in strict mode code within eval code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("'use strict'; function _13_0_15_fun() {eval = 42;};");
            _13_0_15_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
