/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14.1/12.14.1-1-s.js
 * @description Strict Mode - SyntaxError is thrown if a TryStatement with a Catch occurs within strict code and the Identifier of the Catch production is eval
 * @onlyStrict
 */


function testcase() {
        "use strict";

        try {
            eval("\
                   try {} catch (eval) { }\
            ");
            return false;
        } catch (e) {
            return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
