// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * Block within a "while" Expression is not allowed
 *
 * @path ch12/12.6/12.6.2/S12.6.2_A15.js
 * @description Expression is "{0}"
 * @negative
 */

//////////////////////////////////////////////////////////////////////////////
//CHECK#
while({1}){
   break ;
};
//
//////////////////////////////////////////////////////////////////////////////

