/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10.1/12.10.1-8-s.js
 * @description with statement in strict mode throws SyntaxError (function expression, where the container Function is strict)
 * @onlyStrict
 */


function testcase() {
  try {
    Function("\
              \'use strict\'; \
              var f1 = function () {\
                  var o = {}; \
                  with (o) {}; \
                }\
            ");
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
