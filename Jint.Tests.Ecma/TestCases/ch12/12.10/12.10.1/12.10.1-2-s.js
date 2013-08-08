/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-2-s.js
 * @description with statement in strict mode throws SyntaxError (nested function where container is strict)
 * @onlyStrict
 */


function testcase() {
  try {
    // wrapping it in eval since this needs to be a syntax error. The
    // exception thrown must be a SyntaxError exception.
    eval("\
          function foo() {\
            \'use strict\'; \
            function f() {\
                var o = {}; \
                with (o) {};\
            }\
          }\
        ");
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
