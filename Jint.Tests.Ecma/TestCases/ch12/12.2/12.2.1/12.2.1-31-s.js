/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-31-s.js
 * @description eval as local var identifier defined twice throws SyntaxError in strict mode
 * @onlyStrict
 */




function testcase() {
  'use strict';

  try {
    eval('function foo() { var eval, eval;}');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
}
runTestCase(testcase);