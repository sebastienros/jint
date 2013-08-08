/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-16-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when a FunctionExpression is contained in strict mode code within eval code
 * @onlyStrict
 */


function testcase() {

        try {
            eval("'use strict'; var _13_0_16_fun = function () {eval = 42;};");
            _13_0_16_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
