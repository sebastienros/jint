// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Syntax constructions of switch statement
 *
 * @path ch12/12.11/S12.11_A3_T2.js
 * @description Checking if execution of "switch {}" fails
 * @negative
 */

function SwitchTest(value){
  var result = 0;
  
  switch {
    case 0:
      result += 2;
    default:
      result += 32;
      break;
  }
  
  return result;
}

var x = SwitchTest(0);

