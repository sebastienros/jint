/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-10-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when the code of this FunctionBody with an inner function contains a Use Strict Directive
 * @onlyStrict
 */


function testcase() {

        function _13_0_10_fun() {
            function _13_0_10_inner() {
                "use strict";
                eval("eval = 42;");
            }
            _13_0_10_inner();
        };
        try {
            _13_0_10_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
