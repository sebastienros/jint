/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.2/12.2.1/12.2.1-19-s.js
 * @description A direct eval assigning into 'arguments' throws SyntaxError in strict mode
 * @onlyStrict
 */




function testcase() {
  'use strict';

  try {
    eval('arguments = 42;');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError) ;
  }
}
runTestCase(testcase);