/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 13; 
 * The production FunctionBody : SourceElementsopt is evaluated as follows:
 *
 * @path ch13/13.0/13.0-14-s.js
 * @description Strict Mode - SourceElements is evaluated as strict mode code when the function body of a Function constructor contains a Strict Directive
 * @onlyStrict
 */


function testcase() {

        try {
            var _13_0_14_fun = new Function(" ", "'use strict'; eval = 42; ");
            _13_0_14_fun();
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
