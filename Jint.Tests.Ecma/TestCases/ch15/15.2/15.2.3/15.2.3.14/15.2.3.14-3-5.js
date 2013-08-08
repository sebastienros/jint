/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-5.js
 * @description Object.keys must return a fresh array on each invocation
 */


function testcase() {
  var literal = {a: 1};
  var keysBefore = Object.keys(literal);
  if (keysBefore[0] != 'a') return false;
  keysBefore[0] = 'x';
  var keysAfter = Object.keys(literal);
  return (keysBefore[0] == 'x') && (keysAfter[0] == 'a');
 }
runTestCase(testcase);
