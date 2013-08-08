/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-9-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when a FunctionDeclaration that is contained in strict mode code has an inner function
 * @onlyStrict
 */


function testcase() {
        "use strict";

        var _13_0_9_fun = function () {
            function _13_0_9_inner() { eval("eval = 42;"); }
            _13_0_9_inner();
        };
        try {
            _13_0_9_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
