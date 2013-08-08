/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-11-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when the code of this FunctionBody with an inner function which is in strict mode
 * @onlyStrict
 */


function testcase() {

        function _13_0_11_fun() {
            "use strict";
            function _13_0_11_inner() {
                eval("eval = 42;");
            }
            _13_0_11_inner();
        };
        try {
            _13_0_11_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
